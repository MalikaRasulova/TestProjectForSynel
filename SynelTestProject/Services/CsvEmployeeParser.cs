using System.Text;
using Microsoft.VisualBasic.FileIO;
using SynelTestProject.Models;

namespace SynelTestProject.Services;

public sealed class CsvEmployeeParser : ICsvEmployeeParser
{
    public Task<EmployeeImportTable> ParseAsync(Stream csvStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(csvStream);

        using var reader = new StreamReader(csvStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
        using var parser = new TextFieldParser(reader)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true
        };

        parser.SetDelimiters(",");

        if (parser.EndOfData)
        {
            throw new InvalidOperationException("The uploaded CSV file is empty.");
        }

        var headerRow = parser.ReadFields();
        if (headerRow is null || headerRow.Length == 0)
        {
            throw new InvalidOperationException("The uploaded CSV file does not contain a header row.");
        }

        var columns = BuildColumns(headerRow);
        var rows = new List<IReadOnlyDictionary<string, string?>>();

        while (!parser.EndOfData)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fields = parser.ReadFields();
            if (fields is null || fields.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < columns.Count; index++)
            {
                row[columns[index].DatabaseName] = index < fields.Length ? NormalizeValue(fields[index]) : null;
            }

            rows.Add(row);
        }

        return Task.FromResult(new EmployeeImportTable
        {
            Columns = columns,
            Rows = rows
        });
    }

    public static IReadOnlyList<EmployeeColumnDefinition> BuildColumns(IReadOnlyList<string> headers)
    {
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var columns = new List<EmployeeColumnDefinition>(headers.Count);

        foreach (var header in headers)
        {
            var sourceName = string.IsNullOrWhiteSpace(header) ? "Column" : header.Trim();
            var databaseName = BuildSafeColumnName(sourceName, usedNames);
            columns.Add(new EmployeeColumnDefinition(sourceName, databaseName));
        }

        return columns;
    }

    internal static string BuildSafeColumnName(string header, ISet<string>? usedNames = null)
    {
        // Uploaded headers become SQL column names, so normalize them to identifier-safe values.
        var buffer = new StringBuilder(header.Length);

        foreach (var character in header)
        {
            buffer.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        var candidate = buffer.ToString().Trim('_');
        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = "Column";
        }

        if (char.IsDigit(candidate[0]))
        {
            candidate = $"Column_{candidate}";
        }

        if (candidate.Equals("EmployeeId", StringComparison.OrdinalIgnoreCase))
        {
            candidate = "Employee_Id";
        }

        if (usedNames is null)
        {
            return candidate;
        }

        var uniqueCandidate = candidate;
        var suffix = 1;
        while (!usedNames.Add(uniqueCandidate))
        {
            suffix++;
            uniqueCandidate = $"{candidate}_{suffix}";
        }

        return uniqueCandidate;
    }

    private static string? NormalizeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
