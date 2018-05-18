using SQLite;
using System;

namespace Administrator.Services.Database.Models
{
    public enum NoteSearchType
    {
        All,
        Receiver,
        Issuer
    }

    [Table("ModNotes")]
    public class ModNote : IDbModel
    {
        [PrimaryKey]
        [AutoIncrement]
        [Column("NoteId")]
        public long Id { get; set; }

        public DateTimeOffset TimeGiven { get; set; } = DateTimeOffset.UtcNow;

        public long ReceiverId { get; set; }

        public long IssuerId { get; set; }

        public string Note { get; set; }
    }
}