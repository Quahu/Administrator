using System;
using System.Collections.Generic;
using System.Text;
using Administrator.Common;
using Discord;

namespace Administrator.Common.Database.Models
{
    public abstract class Infraction
    {
        public uint Id { get; set; }

        public ulong ReceieverId { get; set; }

        public string ReceieverName { get; set; }

        public ulong IssuerId { get; set; }

        public string IssuerName { get; set; }

        public string Reason { get; set; }

        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        public bool HasBeenAppealed
            => !string.IsNullOrWhiteSpace(AppealMessage);

        public bool HasBeenRevoked { get; set; }

        public ulong RevokerId { get; set; }

        public string RevokerName { get; set; }

        public DateTimeOffset RevocationTimestamp { get; set; }

        public DateTimeOffset AppealedTimestamp { get; set; }

        public string AppealMessage { get; set; }

        public ulong GuildId { get; set; }
    }
}
