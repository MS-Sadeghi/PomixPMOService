using IdentityManagementSystem.UI.Areas.AccessControlReports.Services;
using IdentityManagementSystem.UI.Areas.AccessControlReports.ViewModel;
using Microsoft.AspNetCore.Mvc;

namespace IdentityManagementSystem.UI.Controllers
{
	[Area("AccessControlReports")]
	public class ReportController : Controller
	{

		private readonly IAccessControlReportService _service;

		public ReportController(IAccessControlReportService service)
		{
			_service = service;
		}

		#region GetData

		[HttpGet]
		public IActionResult GetDataReport()
		{
			return View(new GetDataReportPageViewModel());
		}

		[HttpPost]
		public async Task<IActionResult> GetDataReportAjax(GetDataFilterViewModel filter)
		{
			try
			{
				var result = await _service.GetDataAsync(filter);

				return Json(new
				{
					success = true,
					data = result
				});
			}
			catch (Exception ex)
			{
				return BadRequest(new
				{
					success = false,
					message = ex.Message
				});
			}
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
			try
			{
				model.Reports = await _service.GetSumAsync(model.Filter);
			}
			catch (Exception ex)
			{
				ViewBag.ErrorMessage = ex.Message;
			}

			return View(model);
		}

		[HttpPost]
		public async Task<IActionResult> GetSumReportAjax(BaseReportFilterViewModel filter)
		{
			return await ExecuteReportAsync(() => _service.GetSumAsync(filter));
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
			try
			{
				model.Reports = await _service.TrafficByTypeAsync(model.Filter);
			}
			catch (Exception ex)
			{
				ViewBag.ErrorMessage = ex.Message;
			}

			return View(model);
		}

		[HttpPost]
		public async Task<IActionResult> TrafficByTypeReportAjax(TrafficByTypeFilterViewModel filter)
		{
			return await ExecuteReportAsync(() => _service.TrafficByTypeAsync(filter));
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
			try
			{
				model.Reports = await _service.TrafficByPlatesAsync(model.Filter);
			}
			catch (Exception ex)
			{
				ViewBag.ErrorMessage = ex.Message;
			}

			return View(model);
		}

		[HttpPost]
		public async Task<IActionResult> TrafficByPlatesReportAjax(TrafficByPlatesFilterViewModel filter)
		{
			return await ExecuteReportAsync(() => _service.TrafficByPlatesAsync(filter));
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
			try
			{
				model.Reports = await _service.TrafficByNationalIdAsync(model.Filter);
			}
			catch (Exception ex)
			{
				ViewBag.ErrorMessage = ex.Message;
			}

			return View(model);
		}

		[HttpPost]
		public async Task<IActionResult> TrafficByNationalIDReportAjax(TrafficByNationalIdFilterViewModel filter)
		{
			return await ExecuteReportAsync(() => _service.TrafficByNationalIdAsync(filter));
		}

		#endregion

		#region Dashboard
		[HttpGet]
		public IActionResult Dashboard()
		{
			return View();
		}
		[HttpPost]
		public async Task<IActionResult> GetDashboardAjax(string period = "today", bool forceRefresh = false)
		{
			try
			{
				var result = await _service.GetDashboardAsync(period, forceRefresh);

				return Json(new
				{
					success = true,
					data = result
				});
			}
			catch (Exception ex)
			{
				return BadRequest(new
				{
					success = false,
					message = ex.Message
				});
			}
		}
		#endregion

		private async Task<IActionResult> ExecuteReportAsync<T>(Func<Task<List<T>>> action)
		{
			try
			{
				var result = await action();

				return Json(new
				{
					success = true,
					data = result
				});
			}
			catch (Exception ex)
			{
				return BadRequest(new
				{
					success = false,
					message = ex.Message
				});
			}
		}
	}
}