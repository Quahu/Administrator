using System;
using System.Collections.Generic;
using System.Linq;

namespace Administrator.Extensions
{
    public static class StringExtensions
    {
        public static string ToUpperFirst(this string str)
        {
            if (str is null || str.Length == 0) return null;
            if (str.Length == 1) return str.ToUpper();
            return str[0].ToString().ToUpper() + str.Substring(1).ToLower();
        }

        public static bool ContainsWord(this string str, string toCheck)
        {
            str = str.ToLower();
            toCheck = toCheck.ToLower();

            if (str.Equals(toCheck)) return true;
            if (str.StartsWith(toCheck + " ")) return true;
            if (str.EndsWith(" " + toCheck)) return true;
            if (str.Contains(" " + toCheck + " ")) return true;

            return false;
        }

        public static bool TryExtractUri(this string str, out string updated, out Uri uri)
        {
            var strList = new List<string>(str.Split(' '));
            foreach (var s in strList)
                if (Uri.TryCreate(s, UriKind.Absolute, out uri)
                    && (uri.Scheme.Equals(Uri.UriSchemeHttp) || uri.Scheme.Equals(Uri.UriSchemeHttps)))
                {
                    strList.Remove(s);
                    updated = string.Join(' ', strList);
                    return true;
                }

            updated = str;
            uri = null;
            return false;
        }

        public static string SanitizeMentions(this string str)
        {
            return str.Replace("@everyone", "@everyοne").Replace("@here", "@һere");
        }

        // The behemoth.
        public static List<string> GetBestMatchesFor(IEnumerable<string> toCompare, string query, int numResults)
        {
            var comparisonScores = new SortedDictionary<string, int>();

            foreach (var s in toCompare.Distinct())
            {
                var score = query.Length;

                if (s.ToLower().Contains(query.ToLower()))
                    score -= 2 * query.Length;

                foreach (var t in query.Split(' '))
                    if (s.ToLower().Contains(t.ToLower()))
                        score -= t.Length;

                score += ComputeLevenshteinDistance(query, s);

                comparisonScores.Add(s, score);
            }

            var sorted = comparisonScores.OrderBy(c => c.Value);
            return sorted.ToDictionary(x => x.Key, x => x.Value).Keys.Take(numResults).ToList();
        }

        private static int ComputeLevenshteinDistance(string s, string t)
        {
            var n = s.Length;
            var m = t.Length;
            var d = new int[n + 1, m + 1];

            // Step 1
            if (n == 0) return m;

            if (m == 0) return n;

            // Step 2
            for (var i = 0; i <= n; d[i, 0] = i++)
            {
            }

            for (var j = 0; j <= m; d[0, j] = j++)
            {
            }

            // Step 3
            for (var i = 1; i <= n; i++)
                //Step 4
            for (var j = 1; j <= m; j++)
            {
                // Step 5
                var cost = t[j - 1] == s[i - 1] ? 0 : 1;

                // Step 6
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }

            // Step 7
            return d[n, m];
        }

        public static bool EndsWith(this string str, params string[] check)
        {
            var temp = false;
            foreach (var c in check)
                if (str.EndsWith(c))
                    temp = true;
            return temp;
        }

        /*
        private static string Sanitize(string input)
        {
            StringBuilder sb = new StringBuilder();
            input = input.ToLower();

            // if the char is 0-9 or a-z, include it
            foreach (char c in input)
            {
                if ((c >= '0' && c <= '9') || (c >= 'a' && c <= 'z'))
                    sb.Append(c);
            }

            return sb.ToString();
        }

        public static bool StartsWith(this string s, char[] letters)
        {
            foreach (char l in letters)
            {
                if (s.StartsWith(l))
                    return true;
            }
            return false;
        }
        */
    }
}