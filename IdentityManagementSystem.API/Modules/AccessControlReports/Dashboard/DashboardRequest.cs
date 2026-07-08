using Microsoft.AspNetCore.Mvc;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.Dashboard
{
    public class DashboardRequest : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
