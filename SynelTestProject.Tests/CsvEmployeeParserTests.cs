using System.Text;
using SynelTestProject.Services;
using Xunit;

namespace SynelTestProject.Tests;

public sealed class CsvEmployeeParserTests
{
    private readonly CsvEmployeeParser _parser = new();

    [Fact]
    public async Task ParseAsync_ParsesHeadersAndRows()
    {
        const string csv = """
                           Surname,Name,Department
                           Brown,Alice,HR
                           Smith,Bob,IT
                           """;

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await _parser.ParseAsync(stream);

        Assert.Collection(result.Columns,
            column => Assert.Equal("Surname", column.DatabaseName),
            column => Assert.Equal("Name", column.DatabaseName),
            column => Assert.Equal("Department", column.DatabaseName));
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("Brown", result.Rows[0]["Surname"]);
        Assert.Equal("IT", result.Rows[1]["Department"]);
    }

    [Fact]
    public async Task ParseAsync_NormalizesBlankValuesToNull()
    {
        const string csv = """
                           Surname,Name,Department
                           Brown, Alice ,
                           """;

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await _parser.ParseAsync(stream);

        Assert.Single(result.Rows);
        Assert.Equal("Alice", result.Rows[0]["Name"]);
        Assert.Null(result.Rows[0]["Department"]);
    }

    [Fact]
    public void BuildColumns_MakesDuplicateAndUnsafeHeadersUnique()
    {
        var columns = CsvEmployeeParser.BuildColumns(new[] { "Surname", "Surname", "Job Title", "123", "EmployeeId" });

        Assert.Collection(columns,
            column => Assert.Equal("Surname", column.DatabaseName),
            column => Assert.Equal("Surname_2", column.DatabaseName),
            column => Assert.Equal("Job_Title", column.DatabaseName),
            column => Assert.Equal("Column_123", column.DatabaseName),
            column => Assert.Equal("Employee_Id", column.DatabaseName));
    }

    [Fact]
    public async Task ParseAsync_ThrowsForEmptyFile()
    {
        await using var stream = new MemoryStream(Array.Empty<byte>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _parser.ParseAsync(stream));

        Assert.Equal("The uploaded CSV file is empty.", exception.Message);
    }
}
