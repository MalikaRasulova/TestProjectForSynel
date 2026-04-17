namespace SynelTestProject.Models;

public sealed class EmployeeImportResult
{
    public required int RowsProcessed { get; init; }

    public required IReadOnlyList<EmployeeColumnDefinition> Columns { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
}
