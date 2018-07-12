using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Discord;

namespace Administrator.Extensions
{
    public static class StringExtensions
    {
        public static bool IsEmoji(this string str)
            => Regex.IsMatch(str, @"[^\u0000-\u007F]+");

        public static bool IsImageUrl(this string str)
            => str.EndsWith("png", StringComparison.OrdinalIgnoreCase)
               || str.EndsWith("jpg", StringComparison.OrdinalIgnoreCase)
               || str.EndsWith("jpeg", StringComparison.OrdinalIgnoreCase)
               || str.EndsWith("gif", StringComparison.OrdinalIgnoreCase)
               || str.EndsWith("bmp", StringComparison.OrdinalIgnoreCase);

        public static IEnumerable<TEnum> GetEnumFlags<TEnum>() where TEnum : Enum
            => Enum.GetValues(typeof(TEnum)).Cast<TEnum>();

        public static IEnumerable<int> GetUnicodeCodePoints(string emojiString)
        {
            var codePoints = new List<int>(emojiString.Length);
            for (var i = 0; i < emojiString.Length; i++)
            {
                var codePoint = char.ConvertToUtf32(emojiString, i);
                if (codePoint != 0xfe0f)
                    codePoints.Add(codePoint);
                if (char.IsHighSurrogate(emojiString[i]))
                    i++;
            }

            return codePoints;
        }

        public static string FormatPlaceHolders(this string str, IUser user = null, IGuild guild = null,
            ITextChannel channel = null)
        {
            if (user is IUser u)
            {
                str = str.Replace("{user}", u.ToString())
                    .Replace("{user.mention}", u.Mention)
                    .Replace("{user.id}", u.Id.ToString())
                    .Replace("{user.name}", u.Username)
                    .Replace("{user.discrim}", u.Discriminator);
            }

            if (guild is IGuild g)
            {
                str = str.Replace("{guild}", g.ToString())
                    .Replace("{guild.name}", g.Name)
                    .Replace("{guild.id}", g.Id.ToString());
            }

            if (channel is ITextChannel c)
            {
                str = str.Replace("{channel}", c.ToString())
                    .Replace("{channel.mention}", c.Mention)
                    .Replace("{channel.name}", c.Name)
                    .Replace("{channel.id}", c.Id.ToString())
                    .Replace("{channel.topic}", c.Topic);
            }

            return str;
        }

        public static string CreateOrdinal(int num)
        {
            if( num <= 0 ) return num.ToString();

            switch(num % 100)
            {
                case 11:
                case 12:
                case 13:
                    return num + "th";
            }

            switch(num % 10)
            {
                case 1:
                    return num + "st";
                case 2:
                    return num + "nd";
                case 3:
                    return num + "rd";
                default:
                    return num + "th";
            }
        }

        public static string FormatTimeSpan(TimeSpan ts)
        {
            var s = string.Empty;
            if (ts.Days / 7D > 1)
            {
                s += $"{ts.Days / 7} weeks, {ts.Days % 7} days, ";
            }
            else if (ts.Days > 0)
            {
                s += $"{ts.Days} days, ";
            }

            if (ts.Hours > 0)
            {
                s += $"{ts.Hours} hours, ";
            }

            if (ts.Minutes > 0)
            {
                s += $"{ts.Minutes} minutes, ";
            }

            if (ts.Seconds > 0)
            {
                s += $"{ts.Seconds} seconds";
            }

            return s.TrimEnd(' ', ',');
        }
    }
}
