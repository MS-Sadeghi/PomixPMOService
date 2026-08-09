using System.Text.Json.Serialization;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.GetData
{
    public class GetDataResponse
    {
        public string ReportDate { get; set; }

        public string EntranceType { get; set; }

        public int Transit { get; set; }

        public int IranianTruck { get; set; }

        public int IranianPassenger { get; set; }

        public int RecordCount => Transit + IranianTruck + IranianPassenger;
    }

    /// <summary>
    /// شکل خام پاسخ bsr-GetData: به‌جای یک لیست تخت، یک شیء شامل plateData
    /// (هر مسیر/گیت با شمارش روزانه‌اش) برمی‌گرداند. این کلاس‌ها فقط برای
    /// دیسریالایز کردن پاسخ خام استفاده می‌شوند؛ Handler آن را به لیست تخت
    /// (GetDataResponse) تبدیل می‌کند.
    /// </summary>
    internal class GetDataRawResponse
    {
        [JsonPropertyName("plateData")]
        public List<GetDataRawPlateEntry> PlateData { get; set; } = new();
    }

    internal class GetDataRawPlateEntry
    {
        [JsonPropertyName("entranceType")]
        public string EntranceType { get; set; }

        [JsonPropertyName("dailyCounts")]
        public Dictionary<string, GetDataRawDailyCount> DailyCounts { get; set; } = new();
    }

    internal class GetDataRawDailyCount
    {
        [JsonPropertyName("transit")]
        public int Transit { get; set; }

        [JsonPropertyName("iranianTruck")]
        public int IranianTruck { get; set; }

        [JsonPropertyName("iranianPassenger")]
        public int IranianPassenger { get; set; }
    }
}
