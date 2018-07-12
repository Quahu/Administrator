using System;
using System.Collections.Generic;
using System.Text;

namespace Administrator.Common.Database.Models
{
    public class WarningPunishment
    {
        public uint Id { get; set; }

        public ulong GuildId { get; set; }

        public uint Count { get; set; }

        public PunishmentType Type { get; set; }

        public TimeSpan? MuteDuration { get; set; }
    }
}
