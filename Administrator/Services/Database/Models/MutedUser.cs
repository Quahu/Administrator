using System;
using System.Net.Security;
using SQLite;

namespace Administrator.Services.Database.Models
{
    [Table("MutedUsers")]
    public class MutedUser : IDbModel
    {
        [PrimaryKey]
        [AutoIncrement]
        [Column("InternalId")]
        public long Id { get; set; }

        public long UserId { get; set; }

        public long GuildId { get; set; }

        [NotNull]
        public DateTimeOffset Ending { get; set; } = DateTimeOffset.MaxValue;

        [NotNull]
        public string Reason { get; set; } = "No reason specified.";
    }
}