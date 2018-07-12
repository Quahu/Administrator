using Administrator.Common;

namespace Administrator.Common.Database.Models
{
    public class Permission
    {
        public uint Id { get; set; }
        
        public string CommandOrModule { get; set; }
        
        public PermissionFilter Filter { get; set; }
        
        public ulong? TypeId { get; set; }
        
        public PermissionType Type { get; set; }
        
        public Functionality Functionality { get; set; }
        
        public ulong? GuildId { get; set; }
    }
}