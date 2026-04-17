using Microsoft.AspNetCore.Mvc;
using SynelTestProject.Models;
using SynelTestProject.Services;

namespace SynelTestProject.Controllers;

[Route("employees")]
public sealed class EmployeesController : Controller
{
    private readonly ICsvEmployeeParser _csvEmployeeParser;
    private readonly IEmployeeRepository _employeeRepository;

    public EmployeesController(ICsvEmployeeParser csvEmployeeParser, IEmployeeRepository employeeRepository)
    {
        _csvEmployeeParser = csvEmployeeParser;
        _employeeRepository = employeeRepository;
    }

    
    [HttpGet("")]
    public async Task<IActionResult> Get(string? search, CancellationToken cancellationToken)
    {
        var grid = await _employeeRepository.GetGridAsync(search, cancellationToken);
        return Json(grid);
    }

    [HttpPost("import")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Import(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Select a non-empty CSV file before importing." });
        }

        if (!Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only CSV files are supported." });
        }

        await using var stream = file.OpenReadStream();
        var table = await _csvEmployeeParser.ParseAsync(stream, cancellationToken);
        var result = await _employeeRepository.ImportAsync(table, cancellationToken);

        return Json(result);
    }

    [HttpPost("{employeeId:int}")]
    public async Task<IActionResult> Update(int employeeId, [FromBody] EmployeeUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        await _employeeRepository.UpdateAsync(employeeId, request.Values, cancellationToken);
        return Ok();
    }
}
