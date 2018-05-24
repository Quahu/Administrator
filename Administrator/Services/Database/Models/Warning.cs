using System;
using SQLite;

namespace Administrator.Services.Database.Models
{
    [Table("Warnings")]
    public class Warning : IDbModel
    {
        [PrimaryKey]
        [AutoIncrement]
        [Column("InternalId")]
        public long Id { get; set; }

        public DateTimeOffset TimeGiven { get; set; } = DateTimeOffset.UtcNow;

        public long GuildId { get; set; }

        public long ReceiverId { get; set; }

        public long IssuerId { get; set; }

        public string Reason { get; set; }
    }
}