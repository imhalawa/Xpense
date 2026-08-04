namespace Xpense.Services.Helpers
{
    public static class DateTimeExtensions
    {
        /// <summary>
        /// Unix seconds to UTC. Previously converted to local time, which combined with the
        /// local-time writes elsewhere made stored timestamps depend on the server's zone.
        /// </summary>
        public static DateTime? ToDateTime(this long? unixTimeStamp)
        {
            if (!unixTimeStamp.HasValue) return null;
            return DateTimeOffset.FromUnixTimeSeconds(unixTimeStamp.Value).UtcDateTime;
        }
    }
}
