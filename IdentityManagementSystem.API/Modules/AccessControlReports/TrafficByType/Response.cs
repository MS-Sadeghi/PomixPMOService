using System.Text.Json.Serialization;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.TrafficByType
{
    public class TrafficByTypeResponse
    {
        public string ReportDate { get; set; }

        public string EntranceType { get; set; }

        public int Transit { get; set; }

        public int IranianTruck { get; set; }

        public int IranianPassenger { get; set; }

        public int RecordCount => Transit + IranianTruck + IranianPassenger;
    }

    /// <summary>
    /// شکل خام پاسخ bsr-TrafficByType: مشابه bsr-GetData، یک شیء شامل plateData
    /// (هر مسیر/گیت با شمارش روزانه‌اش) برمی‌گرداند، نه یک لیست تخت.
    /// </summary>
    internal class TrafficByTypeRawResponse
    {
        [JsonPropertyName("plateData")]
        public List<TrafficByTypeRawPlateEntry> PlateData { get; set; } = new();
    }

    internal class TrafficByTypeRawPlateEntry
    {
        [JsonPropertyName("entranceType")]
        public string EntranceType { get; set; }

        [JsonPropertyName("dailyCounts")]
        public Dictionary<string, TrafficByTypeRawDailyCount> DailyCounts { get; set; } = new();
    }

    internal class TrafficByTypeRawDailyCount
    {
        [JsonPropertyName("transit")]
        public int Transit { get; set; }

        [JsonPropertyName("iranianTruck")]
        public int IranianTruck { get; set; }

        [JsonPropertyName("iranianPassenger")]
        public int IranianPassenger { get; set; }
    }
}
