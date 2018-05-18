using System;
using SQLite;

namespace Administrator.Services.Database.Models
{
    [Table("AntiSpam")]
    public class AntiSpamChannel : IDbModel
    {
        [PrimaryKey]
        [Column("ChannelId")]
        public long Id { get; set; }

        public long GuildId { get; set; }

        public bool AntiSpamPingEnabled { get; set; } = true;

        public int SpamPingCount { get; set; } = 10;

        public bool AntiSpamMessageEnabled { get; set; } = true;

        public int AntiSpamMessageCount { get; set; } = 10;

        public TimeSpan AntiSpamMessageTimeout { get; set; } = TimeSpan.FromSeconds(5);

        public bool AntiSpamEmoteEnabled { get; set; } = true;

        public int AntiSpamEmoteCount { get; set; } = 10;
    }
}