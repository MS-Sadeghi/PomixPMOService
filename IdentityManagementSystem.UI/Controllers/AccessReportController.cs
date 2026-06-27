using IdentityManagementSystem.UI.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace IdentityManagementSystem.UI.Controllers
{
    public class AccessReportController : Controller
    {
        #region GetData

        [HttpGet]
        public IActionResult GetDataReport()
        {
            return View(new GetDataReportPageViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> GetDataReport(GetDataReportPageViewModel model)
        {
            // Call API : bsr-GetData

            return View(model);
        }

        #endregion

        #region GetSum

        [HttpGet]
        public IActionResult GetSumReport()
        {
            return View(new GetSumReportPageViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> GetSumReport(GetSumReportPageViewModel model)
        {
            // Call API : bsr-GetSum

            return View(model);
        }

        #endregion

        #region TrafficByType

        [HttpGet]
        public IActionResult TrafficByTypeReport()
        {
            return View(new TrafficByTypePageViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> TrafficByTypeReport(TrafficByTypePageViewModel model)
        {
            // Call API : bsr-TrafficByType

            return View(model);
        }

        #endregion

        #region TrafficByPlates

        [HttpGet]
        public IActionResult TrafficByPlatesReport()
        {
            return View(new TrafficByPlatesPageViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> TrafficByPlatesReport(TrafficByPlatesPageViewModel model)
        {
            // Call API : bsr-TrafficByPlates

            return View(model);
        }

        #endregion

        #region TrafficByNationalId

        [HttpGet]
        public IActionResult TrafficByNationalIDReport()
        {
            return View(new TrafficByNationalIdPageViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> TrafficByNationalIDReport(TrafficByNationalIdPageViewModel model)
        {
            // Call API : bsr-TrafficByNationalid

            return View(model);
        }

        #endregion

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
    }
}
