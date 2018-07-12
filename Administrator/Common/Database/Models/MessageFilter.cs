using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Administrator.Common;

namespace Administrator.Common.Database.Models
{
    public class MessageFilter
    {
        public uint Id { get; set; }

        public FilterType Type { get; set; }

        public string Filter { get; set; }

        public bool IsMatch(string str)
        {
            switch (Type)
            {
                case FilterType.Contains:
                    return str.ToLower().Contains(Filter.ToLower());
                case FilterType.Exact:
                    return str.Split(' ').Any(x => x.Equals(Filter, StringComparison.OrdinalIgnoreCase));
                case FilterType.Regex:
                    return Regex.IsMatch(str, Filter);
            }

            return false;
        }

        public ulong GuildId { get; set; }
    }
}
