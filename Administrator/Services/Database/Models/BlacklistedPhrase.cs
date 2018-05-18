using System;
using System.Collections.Generic;
using System.Text;
using SQLite;

namespace Administrator.Services.Database.Models
{
    public enum PhraseBlacklistMatch
    {
        Exact,
        Containing
    }

    [Table("BlacklistedPhrases")]
    public class BlacklistedPhrase : IDbModel
    {
        [PrimaryKey]
        [AutoIncrement]
        [Column("InternalId")]
        public long Id { get; set; }

        public long GuildId { get; set; }

        public string PhraseStr { get; set; } = string.Empty;

        public long Type { get; set; }

        [Ignore]
        public PhraseBlacklistMatch Match
            => (PhraseBlacklistMatch) Type;
    }
}
