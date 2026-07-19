using System.Globalization;
using IdentityManagementSystem.API.Modules.AccessControlReports.Common;
using IdentityManagementSystem.API.Modules.AccessControlReports.GetData;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.Dashboard
{
	public class DashboardHandler
	{
		private readonly IPomixClient _pomixClient;
		private readonly IConfiguration _configuration;

		public DashboardHandler(
			IPomixClient pomixClient,
			IConfiguration configuration)
		{
			_pomixClient = pomixClient;
			_configuration = configuration;
		}

		public async Task<DashboardResponse> HandleAsync()
		{
			var today = DateTime.Now;

			var todayData = await GetTrafficAsync(
				today,
				today,
				new List<string> { "Vehicle", "Person" }
			);

			var weeklyTraffic = new List<ChartItemResponse>();

			for (var i = 6; i >= 0; i--)
			{
				var date = today.AddDays(-i);

				var dayData = await GetTrafficAsync(
					date,
					date,
					new List<string> { "Vehicle", "Person" }
				);

				weeklyTraffic.Add(new ChartItemResponse
				{
					Label = date.ToString("dddd"),
					Value = dayData.Sum(x => x.RecordCount)
				});
			}

			var vehicleCount = todayData
				.Where(x => x.EntranceType == "Vehicle")
				.Sum(x => x.RecordCount);

			var peopleCount = todayData
				.Where(x => x.EntranceType == "Person")
				.Sum(x => x.RecordCount);

			return new DashboardResponse
			{
				TotalTrafficToday = todayData.Sum(x => x.RecordCount),
				VehicleTrafficToday = vehicleCount,
				PeopleTrafficToday = peopleCount,

				// تا وقتی لاگ گزارش‌های کاربران نداری، این عدد واقعی قابل محاسبه نیست.
				ReportsCountToday = 0,

				WeeklyTraffic = weeklyTraffic,

				TrafficTypes = new List<ChartItemResponse>
				{
					new()
					{
						Label = "خودرو",
						Value = vehicleCount
					},
					new()
					{
						Label = "افراد",
						Value = peopleCount
					}
				},

				LastUpdated = DateTime.Now.ToString("HH:mm")
			};
		}

		private async Task<List<GetDataResponse>> GetTrafficAsync(
			DateTime startDate,
			DateTime endDate,
			List<string> entranceTypes)
		{
			var parameters = new object[]
			{
				new
				{
				parameterName = "StartDate",
				parameterValue = ToPersianDate(startDate)
				},
				new
				{
				parameterName = "EndDate",
				parameterValue = ToPersianDate(endDate)
				},
				new
				{
					parameterName = "EntranceTypes",
					parameterValue = entranceTypes
				},
				new
				{
					parameterName = "startTime",
					parameterValue = "00:00"
				},
				new
				{
					parameterName = "endTime",
					parameterValue = "23:59"
				},
				new
				{
					parameterName = "credentials",
					parameterValue = new
					{
						username = _configuration["AccessControl:Username"],
						password = _configuration["AccessControl:Password"]
					}
				}
			};

			return await _pomixClient.ExecuteAsync<List<GetDataResponse>>(
				"bsr-GetData",
				parameters
			);
		}
		private static string ToPersianDate(DateTime date)
		{
			var persianCalendar = new PersianCalendar();

			return $"{persianCalendar.GetYear(date):0000}/" +
				   $"{persianCalendar.GetMonth(date):00}/" +
				   $"{persianCalendar.GetDayOfMonth(date):00}";
		}
	}
}