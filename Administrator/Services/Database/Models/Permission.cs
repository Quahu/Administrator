using SQLite;

namespace Administrator.Services.Database.Models
{
    public enum PermissionType
    {
        Disable,
        Enable
    }

    public enum SetType
    {
        Guild,
        Channel,
        Role,
        User
    }

    [Table("Permissions")]
    public class Permission : IDbModel
    {
        [PrimaryKey]
        [AutoIncrement]
        [Column("InternalId")]
        public long Id { get; set; }

        public string CommandName { get; set; }

        public long SetId { get; set; }

        public SetType Set { get; set; }

        public long GuildId { get; set; }

        public long TypeId
            => (long) Type;

        [Ignore]
        public PermissionType Type { get; set; }
    }
}