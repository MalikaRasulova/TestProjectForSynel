using SynelTestProject.Models;

namespace SynelTestProject.Services;

public interface IEmployeeRepository
{
    Task EnsureDatabaseAsync(CancellationToken cancellationToken = default);

    Task<EmployeeImportResult> ImportAsync(EmployeeImportTable table, CancellationToken cancellationToken = default);

    Task<EmployeeGridResponse> GetGridAsync(string? search, CancellationToken cancellationToken = default);

    Task UpdateAsync(int employeeId, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default);
}
