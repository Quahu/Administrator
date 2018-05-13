using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Administrator.Extensions;
using Discord;
using SQLite;

namespace Administrator.Services.Database.Models
{
    [Table("ReactionRoleMessages")]
    public class ReactionRoleMessage : IDbModel
    {
        [PrimaryKey]
        [Column("MessageId")]
        public long Id { get; set; }

        public long ChannelId { get; set; }

        public long GuildId { get; set; }

        [Ignore]
        public IEnumerable<ulong> RoleIds
            => RoleStr.Split(",").Select(ulong.Parse).ToList();

        [NotNull]
        public string RoleStr { get; set; }

        [Ignore]
        public IEnumerable<IEmote> Emotes
            => EmoteStr.Split(" ").Select(DiscordExtensions.GetEmote).ToList();

        [NotNull]
        public string EmoteStr { get; set; }
    }
}
