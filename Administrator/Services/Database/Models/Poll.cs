using SQLite;
using System.Collections.Generic;
using System.Linq;

namespace Administrator.Services.Database.Models
{
    public class Poll : IDbModel
    {
        [PrimaryKey]
        [AutoIncrement]
        [Column("InternalId")]
        public long Id { get; set; }

        public long GuildId { get; set; }

        public string ChoiceStr { get; set; }

        public IReadOnlyCollection<string> Choices
            => ChoiceStr.Split(';').ToList();

        public IReadOnlyCollection<long> Votes { get; set; }
    }
}
