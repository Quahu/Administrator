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

        [NotNull]
        public DateTimeOffset TimeGiven { get; set; } = DateTimeOffset.UtcNow;

        public long ReceiverId { get; set; }

        public long IssuerId { get; set; }

        [NotNull]
        public string Reason { get; set; }
    }
}