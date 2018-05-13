using SQLite;

namespace Administrator.Services.Database.Models
{
    [Table("UserPhrases")]
    public class UserPhrase : IDbModel
    {
        [PrimaryKey]
        [AutoIncrement]
        [Column("UserPhraseId")]
        public long Id { get; set; }

        public long UserId { get; set; }

        public long GuildId { get; set; }

        [NotNull]
        public string Phrase { get; set; }
    }
}