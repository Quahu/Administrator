using SQLite;
using System;

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

        public DateTimeOffset Ending { get; set; } = DateTimeOffset.MaxValue;

        public string Reason { get; set; } = "No reason specified.";
    }
}