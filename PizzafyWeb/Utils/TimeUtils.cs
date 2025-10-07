using System;

namespace PizzafyWeb.Utils
{
    public static class TimeUtils
    {
        private static TimeZoneInfo? _phTz;

        public static TimeZoneInfo PhilippineTimeZone
        {
            get
            {
                if (_phTz != null) return _phTz;
                try
                {
                    // Preferred IANA id (Linux/ICU)
                    _phTz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
                }
                catch
                {
                    try
                    {
                        // Windows id for UTC+8 used by Philippines
                        _phTz = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
                    }
                    catch
                    {
                        // Fallback to local time zone
                        _phTz = TimeZoneInfo.Local;
                    }
                }
                return _phTz!;
            }
        }

        public static DateTime NowPH => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PhilippineTimeZone);

        public static DateTime ToPH(DateTime dt)
        {
            // If already UTC, convert from UTC. If Unspecified/Local, assume UTC (most DB dates are saved as UTC in this app)
            var utc = dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, PhilippineTimeZone);
        }

        public static string FormatPH(DateTime dt, string format)
        {
            return ToPH(dt).ToString(format);
        }
    }
}
