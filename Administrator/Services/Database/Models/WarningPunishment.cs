using SQLite;

namespace Administrator.Services.Database.Models
{
    public enum Punishment
    {
        Mute,
        Kick,
        Softban,
        Ban
    }

    [Table("WarningPunishments")]
    public class WarningPunishment : IDbModel
    {
        [PrimaryKey]
        [AutoIncrement]
        [Column("InternalId")]
        public long Id { get; set; }

        public long GuildId { get; set; }

        public long Count { get; set; }

        [Column("Punishment")]
        public long PunishmentId { get; set; }

        [Ignore]
        public Punishment Punishment
            => (Punishment) PunishmentId;
    }
}