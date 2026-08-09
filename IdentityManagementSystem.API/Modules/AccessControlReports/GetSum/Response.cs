using System.Text.Json;
using System.Text.Json.Serialization;

namespace IdentityManagementSystem.API.Modules.AccessControlReports.GetSum
{
    public class GetSumCategoryStat
    {
        public int Total { get; set; }

        public int? Iranian { get; set; }

        public int? Transit { get; set; }
    }

    [JsonConverter(typeof(GetSumDailyStatConverter))]
    public class GetSumDailyStat
    {
        public string ReportDate { get; set; }

        public GetSumCategoryStat TruckEntrance { get; set; }

        public GetSumCategoryStat TruckEmptyExit { get; set; }

        public GetSumCategoryStat TruckLoadedExit { get; set; }

        public GetSumCategoryStat CarEntrance { get; set; }

        public GetSumCategoryStat CarExit { get; set; }

        public GetSumCategoryStat PedestrianEntrance { get; set; }

        public GetSumCategoryStat PedestrianExit { get; set; }
    }

    public class GetSumResponse
    {
        [JsonPropertyName("daily")]
        public List<GetSumDailyStat> Daily { get; set; } = new();

        [JsonPropertyName("totalPeriod")]
        public GetSumDailyStat TotalPeriod { get; set; }
    }

    /// <summary>
    /// کلیدهای فارسی این سرویس (مثل «مجموع خروج خالی») ممکن است بسته به سیستم سازمان با
    /// نویسه‌ی «ی» عربی یا فارسی ارسال شوند و تطبیق دقیق رشته (JsonPropertyName) را بی‌اثر کند.
    /// این کانورتر به‌جای برابری دقیق، هر کلید را با زیررشته‌های امن (بدون «ی»/«ک») شناسایی می‌کند.
    /// </summary>
    internal class GetSumDailyStatConverter : JsonConverter<GetSumDailyStat>
    {
        public override GetSumDailyStat Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var result = new GetSumDailyStat();

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                var key = property.Name;

                if (string.Equals(key, "reportDate", StringComparison.OrdinalIgnoreCase))
                {
                    result.ReportDate = property.Value.GetString();
                    continue;
                }

                var stat = ParseCategoryStat(property.Value);
                if (stat == null)
                    continue;

                if (key.Contains("نفر", StringComparison.Ordinal))
                {
                    if (key.Contains("ورود", StringComparison.Ordinal))
                        result.PedestrianEntrance = stat;
                    else if (key.Contains("خروج", StringComparison.Ordinal))
                        result.PedestrianExit = stat;
                }
                else if (key.Contains("سوار", StringComparison.Ordinal))
                {
                    if (key.Contains("ورود", StringComparison.Ordinal))
                        result.CarEntrance = stat;
                    else if (key.Contains("خروج", StringComparison.Ordinal))
                        result.CarExit = stat;
                }
                else if (key.Contains("خروج", StringComparison.Ordinal))
                {
                    if (key.Contains("خال", StringComparison.Ordinal))
                        result.TruckEmptyExit = stat;
                    else if (key.Contains("پر", StringComparison.Ordinal))
                        result.TruckLoadedExit = stat;
                }
                else if (key.Contains("ورود", StringComparison.Ordinal))
                {
                    result.TruckEntrance = stat;
                }
            }

            return result;
        }

        public override void Write(Utf8JsonWriter writer, GetSumDailyStat value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            if (value.ReportDate == null)
                writer.WriteNull("reportDate");
            else
                writer.WriteString("reportDate", value.ReportDate);

            WriteCategory(writer, "truckEntrance", value.TruckEntrance);
            WriteCategory(writer, "truckEmptyExit", value.TruckEmptyExit);
            WriteCategory(writer, "truckLoadedExit", value.TruckLoadedExit);
            WriteCategory(writer, "carEntrance", value.CarEntrance);
            WriteCategory(writer, "carExit", value.CarExit);
            WriteCategory(writer, "pedestrianEntrance", value.PedestrianEntrance);
            WriteCategory(writer, "pedestrianExit", value.PedestrianExit);

            writer.WriteEndObject();
        }

        private static void WriteCategory(Utf8JsonWriter writer, string propertyName, GetSumCategoryStat stat)
        {
            if (stat == null)
            {
                writer.WriteNull(propertyName);
                return;
            }

            writer.WritePropertyName(propertyName);
            writer.WriteStartObject();

            writer.WriteNumber("total", stat.Total);

            if (stat.Iranian.HasValue)
                writer.WriteNumber("iranian", stat.Iranian.Value);
            else
                writer.WriteNull("iranian");

            if (stat.Transit.HasValue)
                writer.WriteNumber("transit", stat.Transit.Value);
            else
                writer.WriteNull("transit");

            writer.WriteEndObject();
        }

        private static GetSumCategoryStat ParseCategoryStat(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
                return null;

            var stat = new GetSumCategoryStat();

            if (element.TryGetProperty("total", out var total) && total.ValueKind == JsonValueKind.Number)
                stat.Total = total.GetInt32();

            if (element.TryGetProperty("iranian", out var iranian) && iranian.ValueKind == JsonValueKind.Number)
                stat.Iranian = iranian.GetInt32();

            if (element.TryGetProperty("transit", out var transit) && transit.ValueKind == JsonValueKind.Number)
                stat.Transit = transit.GetInt32();

            return stat;
        }
    }
}
