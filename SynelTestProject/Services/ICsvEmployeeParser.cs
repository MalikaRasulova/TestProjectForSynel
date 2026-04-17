using SynelTestProject.Models;

namespace SynelTestProject.Services;

public interface ICsvEmployeeParser
{
    Task<EmployeeImportTable> ParseAsync(Stream csvStream, CancellationToken cancellationToken = default);
}
