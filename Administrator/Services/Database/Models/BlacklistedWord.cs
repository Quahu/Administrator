using SQLite;

namespace Administrator.Services.Database.Models
{
    public class BlacklistedWord : IDbModel
    {
        [PrimaryKey]
        [AutoIncrement]
        [Column("InternalId")]
        public long Id { get; set; }

        public long GuildId { get; set; }

        [NotNull]
        public string Word { get; set; }
    }
}
