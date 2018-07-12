using System;
using System.Collections.Generic;
using System.Text;

namespace Administrator.Common.Database.Models
{
    public class Warning
    {
        public uint Id { get; set; }

        public ulong GuildId { get; set; }

        public ulong ReceiverId { get; set; }

        public ulong IssuerId { get; set; }

        public string Reason { get; set; }

        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    }
}
