using System.Reflection;
using GSIP.Application.Abstractions;
using GSIP.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GSIP.Web.Controllers;

public sealed class ShellController(ISystemClock clock) : Controller
{
    [HttpGet("/")]
    public IActionResult Index() => View(CreateModel());

    [HttpGet("/login")]
    public IActionResult Login() => View(CreateModel());

    private ShellViewModel CreateModel()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
        return new ShellViewModel(version, "P02", clock.UtcNow);
    }
}
