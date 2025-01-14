﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Administrator.Extensions
{
    public static class DiscordExtensions
    {
        public static string Format(this IUser user, bool bold = true)
            => user is null
                ? null
                : $"{(bold ? Discord.Format.Bold(user.ToString().Sanitize()) : user.ToString().Sanitize())} (`{user.Id}`)";

        public static string Format(this IRole role, bool bold = true)
            => role is null
                ? null
                : $"{(bold ? Discord.Format.Bold(role.Name.Sanitize()) : role.Name.Sanitize())} (`{role.Id}`)";

        public static string Format(this ITextChannel channel)
            => channel is null ? null : $"{channel.Mention} (`{channel.Id}`)";

        public static string Format(this IVoiceChannel channel, bool bold = true)
            => channel is null ? null : $"{(bold ? Discord.Format.Bold(channel.Name.Sanitize()) : channel.Name.Sanitize())} (`{channel.Id}`)";

        public static string Format(this IGuild guild, bool bold = true)
            => guild is null
                ? null
                : $"{(bold ? Discord.Format.Bold(guild.Name.Sanitize()) : guild.Name.Sanitize())} (`{guild.Id}`)";

        public static SocketRole GetHighestRole(this SocketGuildUser user, Func<SocketRole, bool> func)
            => user.Roles.OrderByDescending(x => x.Position).Where(func).FirstOrDefault();

        public static SocketRole GetHighestRole(this SocketGuildUser user)
            => user.Roles.OrderByDescending(x => x.Position).First();

        public static async ValueTask<IUser> GetOrDownloadUserAsync(this DiscordSocketClient client, ulong id)
            => client.GetUser(id) ?? await client.Rest.GetUserAsync(id) as IUser;

        public static string GetAvatarOrDefault(this IUser user, ImageFormat format = ImageFormat.Auto, ushort size = 128)
            => user.GetAvatarUrl(format, size) ?? user.GetDefaultAvatarUrl();
    }
}