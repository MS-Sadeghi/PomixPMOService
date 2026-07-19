namespace IdentityManagementSystem.UI.Helper
{
    public static class PersianDateHelper
    {
        // برای DateTime (غیر nullable)
        public static string ToPersianDate(this DateTime date)
        {
            var pc = new System.Globalization.PersianCalendar();
            return $"{pc.GetYear(date)}/{pc.GetMonth(date):00}/{pc.GetDayOfMonth(date):00}";
        }

        public static string ToPersianTime(this DateTime date)
        {
            try
            {
                var iranTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");

                var utcDate = date.Kind == DateTimeKind.Utc
                    ? date
                    : DateTime.SpecifyKind(date, DateTimeKind.Utc);

                var iranTime = TimeZoneInfo.ConvertTimeFromUtc(utcDate, iranTimeZone);

                return iranTime.ToString("HH:mm:ss");
            }
            catch
            {
                return date.ToString("HH:mm:ss");
            }
        }

        // برای DateTime? (nullable)
        public static string ToPersianDate(this DateTime? date)
        {
            if (!date.HasValue) return "-";
            return date.Value.ToPersianDate();
        }

        public static string ToPersianTime(this DateTime? date)
        {
            if (!date.HasValue) return "-";
            return date.Value.ToPersianTime();
        }
    }
}
