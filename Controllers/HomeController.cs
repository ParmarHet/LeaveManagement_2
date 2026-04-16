using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using LeavePro.Models;
using LeavePro.Constants;

namespace LeavePro.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole(Roles.Admin))
                return RedirectToAction("Dashboard", "Admin");

            if (User.IsInRole(Roles.Manager))
                return RedirectToAction("Dashboard", "Manager");

            if (User.IsInRole(Roles.Employee))
                return RedirectToAction("Dashboard", "Employee");
        }

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
