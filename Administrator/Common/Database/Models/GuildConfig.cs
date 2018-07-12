using System;
using System.Collections.Generic;

namespace Administrator.Common.Database.Models
{
    public class GuildConfig
    {
        public ulong Id { get; set; }

        public string Name { get; set; }

        public string Prefix { get; set; }

        public ulong? PermRoleId { get; set; }

        public Functionality MuteRole { get; set; } = Functionality.Enable;

        public ulong MuteRoleId { get; set; }

        public Functionality LtpRole { get; set; }

        public ulong LtpRoleId { get; set; }

        public TimeSpan? LtpRoleTimeout { get; set; }

        public ulong LogWarnChannelId { get; set; }

        public ulong LogAppealChannelId { get; set; }

        public ulong LogMuteChannelId { get; set; }

        public ulong LogJoinChannelId { get; set; }

        public ulong LogLeaveChannelId { get; set; }

        public ulong LogBanChannelId { get; set; }

        public ulong LogUnbanChannelId { get; set; }

        public ulong LogMessageDeletionChannelId { get; set; }

        public ulong LogMessageUpdatedChannelId { get; set; }

        public Functionality Suggestions { get; set; }

        public ulong SuggestionChannelId { get; set; }

        public Functionality ArchiveSuggestions { get; set; }

        public ulong SuggestionArchiveId { get; set; }

        public Functionality Greetings { get; set; }

        public ulong GreetChannelId { get; set; }

        public TimeSpan? GreetTimeout { get; set; }

        public string GreetMessage { get; set; } = "Welcome to {guild}, {user}!";

        public string UpvoteArrow { get; set; } = "⬆";

        public string DownvoteArrow { get; set; } = "⬇";

        public Functionality TrackRespects { get; set; } = Functionality.Enable;

        public Functionality FilterInvites { get; set; }

        public Functionality VerboseErrors { get; set; }

        public ushort MinimumPhraseLength { get; set; } = 5;

        public string InviteCode { get; set; }
    }
}
