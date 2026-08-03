using System.Globalization;
using IdentityManagementSystem.API.Modules.AccessControlReports.Common;
using IdentityManagementSystem.API.Modules.AccessControlReports.GetData;
using IdentityManagementSystem.API.Modules.AccessControlReports.TrafficByType;
using Microsoft.Extensions.Caching.Memory;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.Dashboard
{
	public class DashboardHandler
	{
		private readonly IPomixClient _pomixClient;
		private readonly IConfiguration _configuration;
		private readonly IMemoryCache _cache;
		private readonly TrafficByTypeHandler _trafficByTypeHandler;

		// سرویس Pomix سقف درخواست ساعتی دارد؛ کش کردن پاسخ داشبورد باعث می‌شود
		// رفرش‌های پشت‌سرهم/بازدید چند کاربر از یک فیلتر، درخواست تازه‌ای به Pomix نزنند.
		private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

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
			IConfiguration configuration,
			IMemoryCache cache,
			TrafficByTypeHandler trafficByTypeHandler)
		{
			_pomixClient = pomixClient;
			_configuration = configuration;
			_cache = cache;
			_trafficByTypeHandler = trafficByTypeHandler;
		}

		public async Task<DashboardResponse> HandleAsync(DashboardRequest request)
		{
			var period = NormalizePeriod(request?.Period);
			var cacheKey = $"dashboard-{period}";

			if (request?.ForceRefresh != true &&
				_cache.TryGetValue(cacheKey, out DashboardResponse? cached) &&
				cached is not null)
			{
				return cached;
			}

			var response = await BuildDashboardAsync(period);
			_cache.Set(cacheKey, response, CacheDuration);

			return response;
		}

		private async Task<DashboardResponse> BuildDashboardAsync(string period)
		{
			var today = DateTime.Now.Date;

			var (rangeStart, rangeEnd) = GetRange(period, today);

			var periodData = await GetTrafficAsync(rangeStart, rangeEnd, AllEntranceTypes);

			// روند تردد - بازه و طول نمودار بر اساس فیلتر انتخابی تغییر می‌کند.
			// برای بازه‌ی ۳۰ روزه به‌جای یک درخواست به ازای هر روز (۳۰ درخواست)،
			// بازه به تکه‌های هفتگی تقسیم می‌شود (حداکثر ۵ درخواست) تا فشار کمتری
			// به سقف درخواست ساعتی Pomix وارد شود.
			var trendAnchor = period == "yesterday" ? today.AddDays(-1) : today;
			var trendDays = period == "last30" ? 30 : 7;

			var weeklyTraffic = trendDays <= 7
				? await BuildDailyTrendAsync(trendAnchor, trendDays)
				: await BuildWeeklyTrendAsync(trendAnchor, trendDays);

			var vehicleCount = periodData
				.Where(x => !PersonEntranceTypes.Contains(x.EntranceType))
				.Sum(x => x.RecordCount);

			var peopleCount = periodData
				.Where(x => PersonEntranceTypes.Contains(x.EntranceType))
				.Sum(x => x.RecordCount);

			// تفکیک سواری/کامیون فقط از سرویس نوع تردد (bsr-TrafficByType) قابل
			// دریافت است؛ گزارش لاین/گیت (bsr-GetData) بالا این تفکیک را ندارد.
			var carCount = await GetTrafficTypeCountAsync(rangeStart, rangeEnd, 1);
			var truckCount = await GetTrafficTypeCountAsync(rangeStart, rangeEnd, 2);

			return new DashboardResponse
			{
				Period = period,
				TotalTrafficToday = periodData.Sum(x => x.RecordCount),
				VehicleTrafficToday = vehicleCount,
				PeopleTrafficToday = peopleCount,

				CarTrafficToday = carCount,
				TruckTrafficToday = truckCount,

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

		private async Task<List<ChartItemResponse>> BuildDailyTrendAsync(DateTime trendAnchor, int trendDays)
		{
			var items = new List<ChartItemResponse>();

			for (var i = trendDays - 1; i >= 0; i--)
			{
				var date = trendAnchor.AddDays(-i);
				var dayData = await GetTrafficAsync(date, date, AllEntranceTypes);

				items.Add(new ChartItemResponse
				{
					Label = date.ToString("dddd", new CultureInfo("fa-IR")),
					Value = dayData.Sum(x => x.RecordCount)
				});
			}

			return items;
		}

		private async Task<List<ChartItemResponse>> BuildWeeklyTrendAsync(DateTime trendAnchor, int trendDays)
		{
			var items = new List<ChartItemResponse>();
			var bucketStart = trendAnchor.AddDays(-(trendDays - 1));

			while (bucketStart <= trendAnchor)
			{
				var bucketEnd = bucketStart.AddDays(6);
				if (bucketEnd > trendAnchor)
					bucketEnd = trendAnchor;

				var bucketData = await GetTrafficAsync(bucketStart, bucketEnd, AllEntranceTypes);

				items.Add(new ChartItemResponse
				{
					Label = bucketStart == bucketEnd
						? ToPersianShortLabel(bucketStart)
						: ToPersianShortLabel(bucketStart) + " تا " + ToPersianShortLabel(bucketEnd),
					Value = bucketData.Sum(x => x.RecordCount)
				});

				bucketStart = bucketEnd.AddDays(1);
			}

			return items;
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

		private async Task<int> GetTrafficTypeCountAsync(DateTime startDate, DateTime endDate, int trafficType)
		{
			var rows = await _trafficByTypeHandler.HandleAsync(new TrafficByTypeRequest
			{
				StartDate = ToPersianDate(startDate),
				EndDate = ToPersianDate(endDate),
				StartTime = "00:00",
				EndTime = "23:59",
				TrafficTypes = new List<int> { trafficType }
			});

			return rows.Sum(x => x.RecordCount);
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