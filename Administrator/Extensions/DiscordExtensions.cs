using Administrator.Common;
using Discord;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Administrator.Extensions
{
    public static class DiscordExtensions
    {
        public static async Task<IUserMessage> EmbedAsync(this IMessageChannel channel, Embed embed)
        {
            return await channel.SendMessageAsync(string.Empty, embed: embed).ConfigureAwait(false);
        }

        public static async Task EmbedAsync(this IMessageChannel channel, Embed embed, TimeSpan timeout)
        {
            var msg = await channel.SendMessageAsync(string.Empty, embed: embed).ConfigureAwait(false);
            await Task.Delay(timeout).ConfigureAwait(false);
            try
            {
                await msg.DeleteAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        }

        public static async Task SendConfirmAsync(this IMessageChannel channel, string message, TimeSpan? deleteAfter = null)
        {
            var msg = await channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithDescription(message).Build())
                .ConfigureAwait(false);

            if (deleteAfter is TimeSpan ts)
            {
                try
                {
                    await Task.Delay(ts).ConfigureAwait(false);
                    await msg.DeleteAsync().ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }
        }

        public static async Task<IUserMessage> SendErrorAsync(this IMessageChannel channel, string error)
            => await channel.EmbedAsync(new EmbedBuilder().WithErrorColor().WithDescription(error).Build())
            .ConfigureAwait(false);

        public static async Task<IUserMessage> SendConfirmAsync(this IMessageChannel channel, string message)
            => await channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithDescription(message).Build())
                .ConfigureAwait(false);

        public static async Task SendErrorAsync(this IMessageChannel channel, string error, TimeSpan? deleteAfter = null)
        {
            var msg = await channel.EmbedAsync(new EmbedBuilder().WithErrorColor().WithDescription(error).Build())
                .ConfigureAwait(false);

            if (deleteAfter is TimeSpan ts)
            {
                try
                {
                    await Task.Delay(ts).ConfigureAwait(false);
                    await msg.DeleteAsync().ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }
        }

        public static bool TryParseTextChannel(this SocketGuild guild, string input, out SocketTextChannel channel)
        {
            channel = null;

            if (ulong.TryParse(input.TrimStart('<', '#').TrimEnd('>'), out var result)
                && guild.TextChannels.FirstOrDefault(x => x.Id == result) is SocketTextChannel c)
            {
                channel = c;
            }

            if (guild.TextChannels.FirstOrDefault(
                x => x.Name.Equals(input, StringComparison.InvariantCultureIgnoreCase)) is SocketTextChannel c2)
            {
                channel = c2;
            }

            return !(channel is null);
        }

        public static async Task SendMessageAsync(this IMessageChannel channel, string text, TimeSpan timeout,
            bool isTTS = false, Embed embed = null, RequestOptions options = null)
        {
            var msg = await channel.SendMessageAsync(text, isTTS, embed, options).ConfigureAwait(false);
            await Task.Delay(timeout).ConfigureAwait(false);
            try
            {
                await msg.DeleteAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        }

        public static IEmote GetEmote(string emoteStr)
            => Emote.TryParse(emoteStr, out var result) ? result as IEmote : new Emoji(emoteStr);

        public static bool TryGetChannelId(this SocketGuild guild, string input, out ulong channelId)
        {
            if (ulong.TryParse(input.TrimStart('<', '#').TrimEnd('>'), out var result) &&
                guild.Channels.Any(x => x.Id == result))
            {
                channelId = result;
                return true;
            }

            if (guild.Channels.Any(x => x.Name.Equals(input.ToLower())))
            {
                channelId = guild.Channels.First(x => x.Name.Equals(input.ToLower())).Id;
                return true;
            }

            channelId = 0;
            return false;
        }

        /*
        public static string AvatarUrl(this IUser usr)
        {
            return string.IsNullOrWhiteSpace(usr.AvatarId)
                ? string.Empty
                : usr.AvatarId.StartsWith("a_")
                    ? $"{DiscordConfig.CDNUrl}avatars/{usr.Id}/{usr.AvatarId}.gif"
                    : $"{DiscordConfig.CDNUrl}avatars/{usr.Id}/{usr.AvatarId}.png";
        }
        */
    }


    public static class EmbedExtensions
    {
        private static readonly Config Config = BotConfig.New();

        public static EmbedBuilder WithOkColor(this EmbedBuilder eb)
            => eb.WithColor(Convert.ToUInt32(Config.Colors.Ok, 16));

        public static EmbedBuilder WithErrorColor(this EmbedBuilder eb)
            => eb.WithColor(Convert.ToUInt32(Config.Colors.Error, 16));

        public static EmbedBuilder WithWarnColor(this EmbedBuilder eb)
            => eb.WithColor(Convert.ToUInt32(Config.Colors.Warn, 16));

        public static EmbedBuilder WithWinColor(this EmbedBuilder eb)
            => eb.WithColor(Convert.ToUInt32(Config.Colors.Win, 16));

        public static EmbedBuilder WithLoseColor(this EmbedBuilder eb)
            => eb.WithColor(Convert.ToUInt32(Config.Colors.Lose, 16));
    }
}