namespace SynelTestProject.Models;

public sealed class EmployeeGridResponse
{
    public required IReadOnlyList<EmployeeColumnDefinition> Columns { get; init; }

    public required IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows { get; init; }
}
