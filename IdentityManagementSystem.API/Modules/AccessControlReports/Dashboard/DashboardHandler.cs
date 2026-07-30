using System.Globalization;
using IdentityManagementSystem.API.Modules.AccessControlReports.Common;
using IdentityManagementSystem.API.Modules.AccessControlReports.GetData;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.Dashboard
{
	public class DashboardHandler
	{
		private readonly IPomixClient _pomixClient;
		private readonly IConfiguration _configuration;

		private static readonly List<string> AllEntranceTypes = new()
		{
			"1", "2", "11", "120", "13", "140",
			"4", "5", "3", "7",
			"19", "22", "23", "12", "14",
			"نفر رو ورودی", "نفر رو خروجی",
			"سواری رو ورود", "سواری رو خروج"
		};

		private static readonly HashSet<string> PersonEntranceTypes = new()
		{
			"نفر رو ورودی", "نفر رو خروجی"
		};

		public DashboardHandler(
			IPomixClient pomixClient,
			IConfiguration configuration)
		{
			_pomixClient = pomixClient;
			_configuration = configuration;
		}

		public async Task<DashboardResponse> HandleAsync(DashboardRequest request)
		{
			var period = NormalizePeriod(request?.Period);
			var today = DateTime.Now.Date;

			var (rangeStart, rangeEnd) = GetRange(period, today);

			var periodData = await GetTrafficAsync(rangeStart, rangeEnd, AllEntranceTypes);

			// روند تردد روزانه - بازه و طول نمودار بر اساس فیلتر انتخابی تغییر می‌کند
			var trendAnchor = period == "yesterday" ? today.AddDays(-1) : today;
			var trendDays = period == "last30" ? 30 : 7;

			var weeklyTraffic = new List<ChartItemResponse>();
			for (var i = trendDays - 1; i >= 0; i--)
			{
				var date = trendAnchor.AddDays(-i);
				var dayData = await GetTrafficAsync(date, date, AllEntranceTypes);

				weeklyTraffic.Add(new ChartItemResponse
				{
					Label = trendDays <= 7
						? date.ToString("dddd", new CultureInfo("fa-IR"))
						: ToPersianShortLabel(date),
					Value = dayData.Sum(x => x.RecordCount)
				});
			}

			var vehicleCount = periodData
				.Where(x => !PersonEntranceTypes.Contains(x.EntranceType))
				.Sum(x => x.RecordCount);

			var peopleCount = periodData
				.Where(x => PersonEntranceTypes.Contains(x.EntranceType))
				.Sum(x => x.RecordCount);

			return new DashboardResponse
			{
				Period = period,
				TotalTrafficToday = periodData.Sum(x => x.RecordCount),
				VehicleTrafficToday = vehicleCount,
				PeopleTrafficToday = peopleCount,

				// تا وقتی لاگ گزارش‌های کاربران نداری، این عدد واقعی قابل محاسبه نیست.
				ReportsCountToday = 0,

				WeeklyTraffic = weeklyTraffic,

				TrafficTypes = new List<ChartItemResponse>
				{
					new() { Label = "خودرو", Value = vehicleCount },
					new() { Label = "افراد", Value = peopleCount }
				},

				LastUpdated = DateTime.Now.ToString("HH:mm")
			};
		}

		private static string NormalizePeriod(string? period)
		{
			return period?.Trim().ToLowerInvariant() switch
			{
				"yesterday" => "yesterday",
				"last7" => "last7",
				"last30" => "last30",
				_ => "today"
			};
		}

		private static (DateTime start, DateTime end) GetRange(string period, DateTime today)
		{
			return period switch
			{
				"yesterday" => (today.AddDays(-1), today.AddDays(-1)),
				"last7" => (today.AddDays(-6), today),
				"last30" => (today.AddDays(-29), today),
				_ => (today, today)
			};
		}

		private async Task<List<GetDataResponse>> GetTrafficAsync(
			DateTime startDate,
			DateTime endDate,
			List<string> entranceTypes)
		{
			var parameters = new object[]
			{
				new { parameterName = "StartDate", parameterValue = ToPersianDate(startDate) },
				new { parameterName = "EndDate", parameterValue = ToPersianDate(endDate) },
				new { parameterName = "EntranceTypes", parameterValue = entranceTypes },
				new { parameterName = "startTime", parameterValue = "00:00" },
				new { parameterName = "endTime", parameterValue = "23:59" },
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

		private static string ToPersianShortLabel(DateTime date)
		{
			var persianCalendar = new PersianCalendar();

			return $"{persianCalendar.GetMonth(date):00}/{persianCalendar.GetDayOfMonth(date):00}";
		}
	}
}