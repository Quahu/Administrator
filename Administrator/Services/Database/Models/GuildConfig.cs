using SQLite;

namespace Administrator.Services.Database.Models
{
    [Table("GuildConfigs")]
    public class GuildConfig : IDbModel
    {
        [PrimaryKey]
        [Column("GuildId")]
        public long Id { get; set; }

        public long LogChannel { get; set; }

        public long SuggestionChannel { get; set; }

        public long PermRole { get; set; }

        public long MuteRole { get; set; }

        public long LookingToPlayRole { get; set; }

        public long LookingToPlayMaxHours { get; set; }

        public bool MentionLtpUsers { get; set; } = true;

        public long SuggestionArchive { get; set; }

        public long GreetChannel { get; set; }

        public bool HasModifedAntispam { get; set; }

        public bool HasModifiedWarningPunishments { get; set; }

        //public bool HasReceivedPermRoleSpiel { get; set; }

        public string UpvoteArrow { get; set; } = "⬆";

        public string DownvoteArrow { get; set; } = "⬇";

        public bool VerboseErrors { get; set; }

        public bool GreetUserOnJoin { get; set; }

        //public bool MentionUserOnJoin { get; set; } = true;

        public string GreetMessage { get; set; } = "Welcome to the server, {user}!";

        public long GreetTimeout { get; set; } = 60;

        public bool EnableRespects { get; set; }

        public bool InviteFiltering { get; set; }

        public long PhraseMinLength { get; set; } = 3;

        public string InviteCode { get; set; } = string.Empty;
    }
}