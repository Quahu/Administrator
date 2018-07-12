using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Administrator.Common;
using Discord;
using Discord.WebSocket;

namespace Administrator.Extensions
{
    public static class EmbedExtensions
    {
        public static EmbedBuilder WithOkColor(this EmbedBuilder eb)
            => eb.WithColor(BotConfig.OkColor);

        public static EmbedBuilder WithErrorColor(this EmbedBuilder eb)
            => eb.WithColor(BotConfig.ErrorColor);

        public static EmbedBuilder WithWarnColor(this EmbedBuilder eb)
            => eb.WithColor(BotConfig.WarnColor);

        public static EmbedBuilder WithWinColor(this EmbedBuilder eb)
            => eb.WithColor(BotConfig.WinColor);

        public static EmbedBuilder WithLoseColor(this EmbedBuilder eb)
            => eb.WithColor(BotConfig.LoseColor);

        public static EmbedBuilder WithModerator(this EmbedBuilder eb, IUser user)
            => eb.WithFooter($"Moderator: {user?.ToString() ?? "UNKNOWN_USER"}", user?.GetAvatarUrl() ?? user?.GetDefaultAvatarUrl());

        public static EmbedBuilder WithHighestRoleColor(this EmbedBuilder eb, SocketGuildUser user)
        {
            var roles = user.Roles.Where(x => x.Id != user.Guild.EveryoneRole.Id && x.Color.RawValue != Color.Default.RawValue).OrderByDescending(x => x.Position)
                .ToList();
            return eb.WithColor(!roles.Any() ? BotConfig.OkColor : roles.First().Color);
        }
    }
}
