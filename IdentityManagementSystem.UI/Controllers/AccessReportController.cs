using IdentityManagementSystem.UI.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace IdentityManagementSystem.UI.Controllers
{
    public class AccessReportController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

    }
}
