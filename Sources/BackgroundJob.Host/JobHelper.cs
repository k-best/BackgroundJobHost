using System;
using System.Globalization;
using Newtonsoft.Json;

namespace BackgroundJob.Host
{
    public static class JobHelper
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        static JobHelper()
        {
        }

        public static string ToJson(object value)
        {
            return value == null ? null : JsonConvert.SerializeObject(value);
        }

        public static T FromJson<T>(string value)
        {
            return value == null ? default(T) : JsonConvert.DeserializeObject<T>(value);
        }

        public static object FromJson(string value, Type type)
        {
            if (type == null)
                throw new ArgumentNullException("type");
            return value == null ? null : JsonConvert.DeserializeObject(value, type);
        }

        public static long ToTimestamp(DateTime value)
        {
            return (long)(value - Epoch).TotalSeconds;
        }

        public static DateTime FromTimestamp(long value)
        {
            return Epoch.AddSeconds(value);
        }

        public static string SerializeDateTime(DateTime value)
        {
            return value.ToString("o", CultureInfo.InvariantCulture);
        }

        public static DateTime DeserializeDateTime(string value)
        {
            long result;
            return long.TryParse(value, out result) ? FromTimestamp(result) : DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        public static DateTime? DeserializeNullableDateTime(string value)
        {
            return string.IsNullOrEmpty(value) ? new DateTime?() : DeserializeDateTime(value);
        }
    }
}