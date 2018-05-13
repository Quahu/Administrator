using System;
using System.Collections.Generic;
using System.Text;

namespace Administrator.Extensions
{
    public static class TimeSpanExtensions
    {
        public static bool TryParseTimeSpan(this string input, out TimeSpan ts)
        {
            //ts = new TimeSpan();

            input = input.ToLower();
            var check = input;

            if (input.Contains("w")
                && int.TryParse(input.Substring(0, input.IndexOf('w')), out var weeks))
            {

                ts = ts.Add(TimeSpan.FromDays(weeks * 7));
                input = input.Substring(input.IndexOf('w') + 1);
            }

            if (input.Contains("d")
                && int.TryParse(input.Substring(0, input.IndexOf('d')), out var days))
            {
                ts = ts.Add(TimeSpan.FromDays(days));
                input = input.Substring(input.IndexOf('d') + 1);
            }

            if (input.Contains("h")
                && int.TryParse(input.Substring(0, input.IndexOf('h')), out var hours))
            {
                ts = ts.Add(TimeSpan.FromHours(hours));
                input = input.Substring(input.IndexOf('h') + 1);
            }

            if (input.Contains("m")
                && int.TryParse(input.Substring(0, input.IndexOf('m')), out var minutes))
            {
                ts = ts.Add(TimeSpan.FromMinutes(minutes));
                input = input.Substring(input.IndexOf('m') + 1);
            }

            return !input.Equals(check);
        }

        public static string FormatTimeSpan(TimeSpan ts)
        {
            return new StringBuilder()
                .Append(ts.Days > 0 ? $"{ts.Days}d " : string.Empty)
                .Append(ts.Hours > 0 ? $"{ts.Hours}h " : string.Empty)
                .Append(ts.Minutes > 0 ? $"{ts.Minutes}m" : string.Empty)
                .ToString();
        }
    }
}
