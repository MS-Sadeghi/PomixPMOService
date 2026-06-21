using IdentityManagementSystem.UI.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace IdentityManagementSystem.UI.Controllers
{
    public class AccessReportController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View(new AccessReportPageViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Index(AccessReportPageViewModel model)
        {
            // اینجا API صدا زده میشه

            model.Reports = new List<AccessReportViewModel>
            {
                new AccessReportViewModel
                {
                    ReportDate = "1405-03-09",
                    EntranceType = "درب شرقی ورود مسیر 1",
                    RecordCount = 491
                }
            };

            return View(model);
        }
    }
}
