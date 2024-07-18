namespace Xpense.Services.Helpers
{
    public static class DateTimeExtensions
    {
        public static DateTime? ToDateTime(this long? unixTimeStamp)
        {
            if (!unixTimeStamp.HasValue) return null;
            var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTime = dateTime.AddSeconds(unixTimeStamp.Value).ToLocalTime();
            return dateTime;
        }
    }
}
