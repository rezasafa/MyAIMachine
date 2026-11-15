using Microsoft.AspNetCore.Mvc;
namespace MyAIMachine.Controllers;
public class HomeController : Controller
{
    public IActionResult Index() => View();
}
