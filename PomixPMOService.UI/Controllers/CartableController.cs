using Microsoft.AspNetCore.Mvc;

namespace PomixPMOService.UI.Controllers
{
    public class CartableController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            // اینجا می‌تونی بعداً از API داده بگیری و پاس بدی به View
            return View();
        }
    }
}
