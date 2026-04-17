using Microsoft.Data.SqlClient;
using SynelTestProject.Models;

namespace SynelTestProject.Services;

public sealed class SqlServerEmployeeRepository : IEmployeeRepository
{
    private readonly string _connectionString;
    private readonly string _databaseName;

    public SqlServerEmployeeRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("EmployeesDatabase")
            ?? throw new InvalidOperationException("Connection string 'EmployeesDatabase' is missing.");

        var builder = new SqlConnectionStringBuilder(_connectionString);
        _databaseName = builder.InitialCatalog;

        if (string.IsNullOrWhiteSpace(_databaseName))
        {
            throw new InvalidOperationException("Connection string 'EmployeesDatabase' must include a database name.");
        }
    }

    public async Task EnsureDatabaseAsync(CancellationToken cancellationToken = default)
    {
        // The exercise requires a single Employees table, so we provision the database/table on demand.
        var builder = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = "master"
        };

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{EscapeLiteral(_databaseName)}') IS NULL CREATE DATABASE [{_databaseName}]";
        await command.ExecuteNonQueryAsync(cancellationToken);

        await using var employeeConnection = new SqlConnection(_connectionString);
        await employeeConnection.OpenAsync(cancellationToken);

        await using var tableCommand = employeeConnection.CreateCommand();
        tableCommand.CommandText = """
                                   IF OBJECT_ID(N'dbo.Employees', N'U') IS NULL
                                   BEGIN
                                       CREATE TABLE [dbo].[Employees]
                                       (
                                           [EmployeeId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY
                                       );
                                   END
                                   """;
        await tableCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<EmployeeImportResult> ImportAsync(EmployeeImportTable table, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);

        await EnsureDatabaseAsync(cancellationToken);
        await EnsureColumnsAsync(table.Columns, cancellationToken);

        if (table.Rows.Count == 0)
        {
            return new EmployeeImportResult
            {
                RowsProcessed = 0,
                Columns = table.Columns,
                Warnings = Array.Empty<string>()
            };
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var row in table.Rows)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqlTransaction)transaction;

            var databaseColumns = table.Columns.Select(column => $"[{column.DatabaseName}]").ToArray();
            var parameterNames = new List<string>(table.Columns.Count);

            for (var index = 0; index < table.Columns.Count; index++)
            {
                var parameterName = $"@value{index}";
                parameterNames.Add(parameterName);
                command.Parameters.AddWithValue(parameterName, row[table.Columns[index].DatabaseName] ?? (object)DBNull.Value);
            }

            command.CommandText = $"INSERT INTO [dbo].[Employees] ({string.Join(", ", databaseColumns)}) VALUES ({string.Join(", ", parameterNames)})";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new EmployeeImportResult
        {
            RowsProcessed = table.Rows.Count,
            Columns = table.Columns,
            Warnings = Array.Empty<string>()
        };
    }

    public async Task<EmployeeGridResponse> GetGridAsync(string? search, CancellationToken cancellationToken = default)
    {
        await EnsureDatabaseAsync(cancellationToken);

        var columns = await GetColumnsAsync(cancellationToken);
        if (columns.Count == 0)
        {
            return new EmployeeGridResponse
            {
                Columns = Array.Empty<EmployeeColumnDefinition>(),
                Rows = Array.Empty<IReadOnlyDictionary<string, string?>>()
            };
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var selectColumns = new List<string> { "[EmployeeId]" };
        selectColumns.AddRange(columns.Select(column => $"[{column.DatabaseName}]"));

        command.CommandText = $"SELECT {string.Join(", ", selectColumns)} FROM [dbo].[Employees]";

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchConditions = columns.Select((column, index) => $"[{column.DatabaseName}] LIKE @search{index}").ToArray();
            command.CommandText += $" WHERE {string.Join(" OR ", searchConditions)}";

            for (var index = 0; index < columns.Count; index++)
            {
                command.Parameters.AddWithValue($"@search{index}", $"%{search.Trim()}%");
            }
        }

        var sortColumn = columns.FirstOrDefault(column =>
            column.SourceName.Equals("Surname", StringComparison.OrdinalIgnoreCase) ||
            column.DatabaseName.Equals("Surname", StringComparison.OrdinalIgnoreCase));

        command.CommandText += $" ORDER BY [{sortColumn?.DatabaseName ?? columns[0].DatabaseName}] ASC";

        var rows = new List<IReadOnlyDictionary<string, string?>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["EmployeeId"] = reader["EmployeeId"].ToString()
            };

            foreach (var column in columns)
            {
                row[column.DatabaseName] = reader[column.DatabaseName] is DBNull ? null : reader[column.DatabaseName].ToString();
            }

            rows.Add(row);
        }

        return new EmployeeGridResponse
        {
            Columns = columns,
            Rows = rows
        };
    }

    public async Task UpdateAsync(int employeeId, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default)
    {
        await EnsureDatabaseAsync(cancellationToken);

        var columns = await GetColumnsAsync(cancellationToken);
        var allowedColumns = new HashSet<string>(columns.Select(column => column.DatabaseName), StringComparer.OrdinalIgnoreCase);
        var updatableValues = values
            .Where(item => allowedColumns.Contains(item.Key))
            .ToList();

        if (updatableValues.Count == 0)
        {
            return;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var assignments = new List<string>(updatableValues.Count);
        for (var index = 0; index < updatableValues.Count; index++)
        {
            var value = updatableValues[index];
            assignments.Add($"[{value.Key}] = @value{index}");
            command.Parameters.AddWithValue($"@value{index}", value.Value ?? (object)DBNull.Value);
        }

        command.Parameters.AddWithValue("@employeeId", employeeId);
        command.CommandText = $"UPDATE [dbo].[Employees] SET {string.Join(", ", assignments)} WHERE [EmployeeId] = @employeeId";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureColumnsAsync(IReadOnlyList<EmployeeColumnDefinition> columns, CancellationToken cancellationToken)
    {
        // New CSV headers expand the Employees table instead of forcing a hard-coded schema.
        var existingColumns = await GetExistingColumnNamesAsync(cancellationToken);
        if (columns.All(column => existingColumns.Contains(column.DatabaseName)))
        {
            return;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var column in columns.Where(column => !existingColumns.Contains(column.DatabaseName)))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER TABLE [dbo].[Employees] ADD [{column.DatabaseName}] NVARCHAR(4000) NULL";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task<IReadOnlyList<EmployeeColumnDefinition>> GetColumnsAsync(CancellationToken cancellationToken)
    {
        var columns = new List<EmployeeColumnDefinition>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT [COLUMN_NAME]
                              FROM [INFORMATION_SCHEMA].[COLUMNS]
                              WHERE [TABLE_SCHEMA] = 'dbo'
                                AND [TABLE_NAME] = 'Employees'
                                AND [COLUMN_NAME] <> 'EmployeeId'
                              ORDER BY [ORDINAL_POSITION]
                              """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var columnName = reader.GetString(0);
            columns.Add(new EmployeeColumnDefinition(columnName, columnName));
        }

        return columns;
    }

    private async Task<HashSet<string>> GetExistingColumnNamesAsync(CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT [COLUMN_NAME]
                              FROM [INFORMATION_SCHEMA].[COLUMNS]
                              WHERE [TABLE_SCHEMA] = 'dbo'
                                AND [TABLE_NAME] = 'Employees'
                              """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static string EscapeLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
