using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SynelTestProject.Models;
using SynelTestProject.Services;

namespace SynelTestProject.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IEmployeeRepository _employeeRepository;

    public HomeController(ILogger<HomeController> logger, IEmployeeRepository employeeRepository)
    {
        _logger = logger;
        _employeeRepository = employeeRepository;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        await _employeeRepository.EnsureDatabaseAsync(cancellationToken);
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
