using System;
using System.Collections.Generic;
using System.Text;
using Administrator.Common;

namespace Administrator.Common.Database.Models
{
    public class DiscordUser
    {
        public ulong Id { get; set; }

        public uint GlobalXp { get; set; }

        public DateTimeOffset LastXpGain { get; set; }

        public uint RespectsPaid { get; set; }

        public DateTimeOffset LastRespectsPaid { get; set; }

        public Functionality AllowSuggestionDms { get; set; } = Functionality.Enable;
    }
}
