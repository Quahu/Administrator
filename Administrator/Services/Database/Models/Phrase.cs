using System;
using SQLite;

namespace Administrator.Services.Database.Models
{
    [Table("Phrases")]
    public class Phrase : IDbModel
    {
        [PrimaryKey]
        [AutoIncrement]
        [Column("InternalId")]
        public long Id { get; set; }

        [NotNull]
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        public long UserPhraseId { get; set; }

        public long UserId { get; set; }

        public long ChannelId { get; set; }
        
        public long GuildId { get; set; }
    }
}