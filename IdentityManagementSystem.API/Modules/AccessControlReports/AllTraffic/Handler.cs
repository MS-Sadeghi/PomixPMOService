using IdentityManagementSystem.API.Modules.AccessControlReports.GetData;
using IdentityManagementSystem.API.Modules.AccessControlReports.TrafficByNationalId;
using IdentityManagementSystem.API.Modules.AccessControlReports.TrafficByPlates;
using IdentityManagementSystem.API.Modules.AccessControlReports.TrafficByType;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.AllTraffic
{
    // پومیکس یک سرویس واحد برای "همه‌ی ترددها" ندارد؛ این هندلر بسته به این‌که
    // کاربر کدام فیلترها را پر کرده، سرویس مناسب را صدا می‌زند:
    // پلاک/کدملی -> ردیف‌های تکی همان تردد؛ مسیر تردد -> گزارش بر اساس گیت/لاین؛
    // در غیر این صورت (حالت پیش‌فرض) -> جمع روزانه به‌تفکیک سواری/کامیون/نفر.
    public class AllTrafficHandler
    {
        private readonly GetDataHandler _getDataHandler;
        private readonly TrafficByTypeHandler _trafficByTypeHandler;
        private readonly TrafficByPlatesHandler _trafficByPlatesHandler;
        private readonly TrafficByNationalIdHandler _trafficByNationalIdHandler;

        public AllTrafficHandler(
            GetDataHandler getDataHandler,
            TrafficByTypeHandler trafficByTypeHandler,
            TrafficByPlatesHandler trafficByPlatesHandler,
            TrafficByNationalIdHandler trafficByNationalIdHandler)
        {
            _getDataHandler = getDataHandler;
            _trafficByTypeHandler = trafficByTypeHandler;
            _trafficByPlatesHandler = trafficByPlatesHandler;
            _trafficByNationalIdHandler = trafficByNationalIdHandler;
        }

        public async Task<AllTrafficResponse> HandleAsync(AllTrafficRequest request)
        {
            var hasPlate =
                !string.IsNullOrWhiteSpace(request.P1) ||
                !string.IsNullOrWhiteSpace(request.P2) ||
                !string.IsNullOrWhiteSpace(request.P3) ||
                !string.IsNullOrWhiteSpace(request.P4);

            if (hasPlate)
            {
                var plateRows = await _trafficByPlatesHandler.HandleAsync(new TrafficByPlatesRequest
                {
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    P1 = request.P1,
                    P2 = request.P2,
                    P3 = request.P3,
                    P4 = request.P4
                });

                return new AllTrafficResponse { Mode = "plates", PlateRows = plateRows };
            }

            if (!string.IsNullOrWhiteSpace(request.NationalId))
            {
                var nationalIdRows = await _trafficByNationalIdHandler.HandleAsync(new TrafficByNationalIdRequest
                {
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    NationalId = request.NationalId
                });

                return new AllTrafficResponse { Mode = "nationalid", NationalIdRows = nationalIdRows };
            }

            if (request.EntranceTypes != null && request.EntranceTypes.Count > 0)
            {
                var laneRows = await _getDataHandler.HandleAsync(new GetDataRequest
                {
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    EntranceTypes = request.EntranceTypes
                });

                return new AllTrafficResponse { Mode = "lanes", LaneRows = laneRows };
            }

            var selectedTypes = request.TrafficTypes != null && request.TrafficTypes.Count > 0
                ? request.TrafficTypes
                : new List<int> { 1, 2, 3 };

            var dailyTotals = new Dictionary<string, AllTrafficDailyRow>();

            foreach (var code in selectedTypes)
            {
                var rows = await _trafficByTypeHandler.HandleAsync(new TrafficByTypeRequest
                {
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    TrafficTypes = new List<int> { code }
                });

                foreach (var row in rows)
                {
                    var dateKey = NormalizeDateKey(row.ReportDate);

                    if (!dailyTotals.TryGetValue(dateKey, out var daily))
                    {
                        daily = new AllTrafficDailyRow { ReportDate = dateKey };
                        dailyTotals[dateKey] = daily;
                    }

                    // سرویس پومیکس: 1=کامیون، 2=سواری، 3=نفر
                    if (code == 1) daily.TruckCount += row.RecordCount;
                    else if (code == 2) daily.CarCount += row.RecordCount;
                    else if (code == 3) daily.PedestrianCount += row.RecordCount;

                    daily.Total += row.RecordCount;
                }
            }

            return new AllTrafficResponse
            {
                Mode = "types",
                DailyRows = dailyTotals.Values.OrderBy(x => x.ReportDate).ToList()
            };
        }

        // پاسخ پومیکس تاریخ را بدون صفر ابتدایی برمی‌گرداند (مثل 1405/5/1)؛
        // برای مرتب‌سازی و merge صحیح بین سه فراخوانی جداگانه، یکدست می‌شود.
        private static string NormalizeDateKey(string reportDate)
        {
            if (string.IsNullOrWhiteSpace(reportDate))
                return reportDate ?? string.Empty;

            var parts = reportDate.Split('/');
            if (parts.Length != 3 ||
                !int.TryParse(parts[1], out var month) ||
                !int.TryParse(parts[2], out var day))
            {
                return reportDate;
            }

            return $"{parts[0]}/{month:00}/{day:00}";
        }
    }
}
