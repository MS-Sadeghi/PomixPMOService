using IdentityManagementSystem.UI.Areas.AccessControlReports.Services;
using IdentityManagementSystem.UI.Areas.AccessControlReports.ViewModel;
using IdentityManagementSystem.UI.ViewModels;

namespace IdentityManagementSystem.API.Services.AccessControlReports
{
	public class AccessControlReportService : IAccessControlReportService
	{
		private readonly IHttpClientFactory _factory;

		public AccessControlReportService(IHttpClientFactory factory)
		{
			_factory = factory;
		}

		public async Task<List<GetDataReportViewModel>> GetDataAsync(GetDataFilterViewModel filter)
		{
			var client = _factory.CreateClient("PomixApi");

			var request = new
			{
				StartDate = filter.StartDate,
				EndDate = filter.EndDate,
				StartTime = filter.StartTime,
				EndTime = filter.EndTime,
				EntranceTypes = filter.EntranceTypes
			};

			var response = await client.PostAsJsonAsync(
				"access-control-reports/get-data",
				request);

			if (!response.IsSuccessStatusCode)
				await ThrowForFailedResponseAsync(response, "خلاصه تردد");

			return await response.Content.ReadFromJsonAsync<List<GetDataReportViewModel>>()
				   ?? new List<GetDataReportViewModel>();
		}

		public async Task<List<GetSumReportViewModel>> GetSumAsync(BaseReportFilterViewModel filter)
		{
			var client = _factory.CreateClient("PomixApi");

			var request = new
			{
				StartDate = filter.StartDate,
				EndDate = filter.EndDate,
				StartTime = filter.StartTime,
				EndTime = filter.EndTime
			};

			var response = await client.PostAsJsonAsync(
				"access-control-reports/get-sum",
				request);

			if (!response.IsSuccessStatusCode)
				await ThrowForFailedResponseAsync(response, "گزارش تجمیعی");

			return await response.Content.ReadFromJsonAsync<List<GetSumReportViewModel>>()
				   ?? new List<GetSumReportViewModel>();
		}

		public async Task<List<TrafficByTypeReportViewModel>> TrafficByTypeAsync(TrafficByTypeFilterViewModel filter)
		{
			var client = _factory.CreateClient("PomixApi");

			var request = new
			{
				StartDate = filter.StartDate,
				EndDate = filter.EndDate,
				StartTime = filter.StartTime,
				EndTime = filter.EndTime,
				TrafficTypes = filter.TrafficTypes
			};

			var response = await client.PostAsJsonAsync(
				"access-control-reports/traffic-by-type",
				request);

			if (!response.IsSuccessStatusCode)
				await ThrowForFailedResponseAsync(response, "تفکیک نوع تردد");

			return await response.Content.ReadFromJsonAsync<List<TrafficByTypeReportViewModel>>()
				   ?? new List<TrafficByTypeReportViewModel>();
		}

		public async Task<List<TrafficByPlatesReportViewModel>> TrafficByPlatesAsync(TrafficByPlatesFilterViewModel filter)
		{
			var client = _factory.CreateClient("PomixApi");

			var request = new
			{
				StartDate = filter.StartDate,
				EndDate = filter.EndDate,
				StartTime = filter.StartTime,
				EndTime = filter.EndTime,
				P1 = filter.P1,
				P2 = filter.P2,
				P3 = filter.P3,
				P4 = filter.P4
			};

			var response = await client.PostAsJsonAsync(
				"access-control-reports/traffic-by-plates",
				request);

			if (!response.IsSuccessStatusCode)
				await ThrowForFailedResponseAsync(response, "تردد بر اساس پلاک");

			return await response.Content.ReadFromJsonAsync<List<TrafficByPlatesReportViewModel>>()
				   ?? new List<TrafficByPlatesReportViewModel>();
		}

		public async Task<List<TrafficByNationalIdReportViewModel>> TrafficByNationalIdAsync(TrafficByNationalIdFilterViewModel filter)
		{
			var client = _factory.CreateClient("PomixApi");

			var request = new
			{
				StartDate = filter.StartDate,
				EndDate = filter.EndDate,
				StartTime = filter.StartTime,
				EndTime = filter.EndTime,
				NationalId = filter.NationalId
			};

			var response = await client.PostAsJsonAsync(
				"access-control-reports/traffic-by-nationalid",
				request);

			if (!response.IsSuccessStatusCode)
				await ThrowForFailedResponseAsync(response, "تردد بر اساس کد ملی");

			return await response.Content.ReadFromJsonAsync<List<TrafficByNationalIdReportViewModel>>()
				   ?? new List<TrafficByNationalIdReportViewModel>();
		}

		public async Task<DashboardResponseViewModel> GetDashboardAsync(string period, bool forceRefresh = false)
		{
			var client = _factory.CreateClient("PomixApi");

			var response = await client.PostAsJsonAsync(
				"access-control-reports/dashboard",
				new { period, forceRefresh }
			);

			if (!response.IsSuccessStatusCode)
				await ThrowForFailedResponseAsync(response, "داشبورد");

			return await response.Content
				.ReadFromJsonAsync<DashboardResponseViewModel>()
				?? new DashboardResponseViewModel();
		}

		private static async Task ThrowForFailedResponseAsync(HttpResponseMessage response, string reportName)
		{
			var errorContent = await response.Content.ReadAsStringAsync();
			throw new InvalidOperationException(
				$"دریافت گزارش «{reportName}» با خطا مواجه شد ({(int)response.StatusCode}): {errorContent}");
		}
	}
}