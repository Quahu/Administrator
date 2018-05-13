using SQLite;
using System;

namespace Administrator.Services.Database.Models
{
    [Table("LtpUsers")]
    public class LtpUser : IDbModel
    {
        [PrimaryKey]
        [AutoIncrement]
        [Column("InternalId")]
        public long Id { get; set; }

        public long UserId { get; set; }

        public long GuildId { get; set; }

        [NotNull]
        public DateTimeOffset Expires { get; set; }
    }
}
