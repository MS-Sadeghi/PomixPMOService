using System.Globalization;
using IdentityManagementSystem.API.Modules.AccessControlReports.GetSum;
using Microsoft.Extensions.Caching.Memory;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.Dashboard
{
	public class DashboardHandler
	{
		private readonly IMemoryCache _cache;
		private readonly GetSumHandler _getSumHandler;

		// سرویس Pomix سقف درخواست ساعتی دارد؛ کش کردن پاسخ داشبورد باعث می‌شود
		// رفرش‌های پشت‌سرهم/بازدید چند کاربر از یک فیلتر، درخواست تازه‌ای به Pomix نزنند.
		private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

		public DashboardHandler(
			IMemoryCache cache,
			GetSumHandler getSumHandler)
		{
			_cache = cache;
			_getSumHandler = getSumHandler;
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

			// روند تردد - بازه‌ی نمودار مستقل از بازه‌ی کارت‌های بالای صفحه است
			// (مثلاً برای period=today همچنان روند ۷ روز اخیر نمایش داده می‌شود).
			var trendAnchor = period == "yesterday" ? today.AddDays(-1) : today;
			var trendDays = period == "last30" ? 30 : 7;
			var trendStart = trendAnchor.AddDays(-(trendDays - 1));

			// یک درخواست bsr-GetSum که بازه‌ی کارت‌ها و بازه‌ی نمودار روند را
			// همزمان پوشش می‌دهد، به‌جای چند درخواست جداگانه (یکی به ازای هر
			// روز/هفته) که پیش‌تر برای GetData/TrafficByType انجام می‌شد.
			var queryStart = rangeStart < trendStart ? rangeStart : trendStart;
			var queryEnd = rangeEnd > trendAnchor ? rangeEnd : trendAnchor;

			var sum = await _getSumHandler.HandleAsync(new GetSumRequest
			{
				StartDate = ToPersianDate(queryStart),
				EndDate = ToPersianDate(queryEnd),
				StartTime = "00:00",
				EndTime = "23:59"
			});

			var byDate = new Dictionary<string, GetSumDailyStat>();
			foreach (var day in sum.Daily ?? new List<GetSumDailyStat>())
			{
				if (day?.ReportDate != null && !byDate.ContainsKey(day.ReportDate))
					byDate[day.ReportDate] = day;
			}

			var periodDates = new HashSet<string>();
			for (var d = rangeStart; d <= rangeEnd; d = d.AddDays(1))
				periodDates.Add(ToPersianDate(d));

			var periodDaily = byDate.Values.Where(d => periodDates.Contains(d.ReportDate)).ToList();

			var truckCount = periodDaily.Sum(d =>
				CategoryTotal(d.TruckEntrance) + CategoryTotal(d.TruckEmptyExit) + CategoryTotal(d.TruckLoadedExit));

			var carCount = periodDaily.Sum(d =>
				CategoryTotal(d.CarEntrance) + CategoryTotal(d.CarExit));

			var peopleCount = periodDaily.Sum(d =>
				CategoryTotal(d.PedestrianEntrance) + CategoryTotal(d.PedestrianExit));

			var vehicleCount = truckCount + carCount;
			var totalTraffic = vehicleCount + peopleCount;

			var weeklyTraffic = trendDays <= 7
				? BuildDailyTrend(byDate, trendAnchor, trendDays)
				: BuildWeeklyTrend(byDate, trendAnchor, trendDays);

			return new DashboardResponse
			{
				Period = period,
				TotalTrafficToday = totalTraffic,
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

		private static List<ChartItemResponse> BuildDailyTrend(
			Dictionary<string, GetSumDailyStat> byDate, DateTime trendAnchor, int trendDays)
		{
			var items = new List<ChartItemResponse>();

			for (var i = trendDays - 1; i >= 0; i--)
			{
				var date = trendAnchor.AddDays(-i);
				byDate.TryGetValue(ToPersianDate(date), out var day);

				items.Add(new ChartItemResponse
				{
					Label = date.ToString("dddd", new CultureInfo("fa-IR")),
					Value = DayTotal(day)
				});
			}

			return items;
		}

		private static List<ChartItemResponse> BuildWeeklyTrend(
			Dictionary<string, GetSumDailyStat> byDate, DateTime trendAnchor, int trendDays)
		{
			var items = new List<ChartItemResponse>();
			var bucketStart = trendAnchor.AddDays(-(trendDays - 1));

			while (bucketStart <= trendAnchor)
			{
				var bucketEnd = bucketStart.AddDays(6);
				if (bucketEnd > trendAnchor)
					bucketEnd = trendAnchor;

				var bucketTotal = 0;
				for (var d = bucketStart; d <= bucketEnd; d = d.AddDays(1))
				{
					byDate.TryGetValue(ToPersianDate(d), out var day);
					bucketTotal += DayTotal(day);
				}

				items.Add(new ChartItemResponse
				{
					Label = bucketStart == bucketEnd
						? ToPersianShortLabel(bucketStart)
						: ToPersianShortLabel(bucketStart) + " تا " + ToPersianShortLabel(bucketEnd),
					Value = bucketTotal
				});

				bucketStart = bucketEnd.AddDays(1);
			}

			return items;
		}

		private static int DayTotal(GetSumDailyStat day)
		{
			if (day == null)
				return 0;

			return CategoryTotal(day.TruckEntrance) + CategoryTotal(day.TruckEmptyExit) + CategoryTotal(day.TruckLoadedExit)
				 + CategoryTotal(day.CarEntrance) + CategoryTotal(day.CarExit)
				 + CategoryTotal(day.PedestrianEntrance) + CategoryTotal(day.PedestrianExit);
		}

		private static int CategoryTotal(GetSumCategoryStat stat) => stat?.Total ?? 0;

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
