using System;
using System.Linq;
using System.Threading.Tasks;
using Administrator.Common;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Administrator.Extensions
{
    public static class DiscordExtensions
    {
        public static async Task<bool> TryDeleteAsync(this IDeletable toDelete)
        {
            try
            {
                await toDelete.DeleteAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<IUserMessage> SendMessageAsync(this IMessageChannel channel, string message,
            TimeSpan? timeout = null)
        {
            var m = await channel.SendMessageAsync(message);

            if (timeout is TimeSpan ts)
            {
                _ = Task.Run(async () => await Task.Delay(ts).ContinueWith(_ => m.DeleteAsync()));
            }

            return m;
        }

        public static async Task<IUserMessage> EmbedAsync(this IMessageChannel channel, Embed embed,
            TimeSpan? timeout = null)
        {
            var m = await channel.SendMessageAsync(string.Empty, embed: embed);

            if (timeout is TimeSpan ts)
            {
                _ = Task.Run(async () => await Task.Delay(ts).ContinueWith(_ => m.DeleteAsync()));
            }

            return m;
        }

        public static async Task<IUserMessage> EmbedAsync(this IMessageChannel channel, TomlEmbed embed,
            TimeSpan? timeout = null)
        {
            var plaintext = string.IsNullOrWhiteSpace(embed.Plaintext) ? string.Empty : embed.Plaintext;
            var eb = new EmbedBuilder();

            if (!string.IsNullOrWhiteSpace(embed.Title))
                eb.WithTitle(embed.Title);

            if (!string.IsNullOrWhiteSpace(embed.Description))
                eb.WithDescription(embed.Description);

            if (!string.IsNullOrWhiteSpace(embed.Color))
                eb.WithColor(Convert.ToUInt32(embed.Color, 16));

            if (!string.IsNullOrWhiteSpace(embed.ThumbnailUrl))
                eb.WithThumbnailUrl(embed.ThumbnailUrl);

            if (!string.IsNullOrWhiteSpace(embed.ImageUrl))
                eb.WithImageUrl(embed.ImageUrl);

            if (embed.Author is Author au)
                eb.WithAuthor(au.Name, au.IconUrl);

            if (embed.Footer is Footer f)
                eb.WithFooter(f.Text, f.IconUrl);

            if (embed.Fields?.Any() == true)
            {
                foreach (var field in embed.Fields)
                {
                    eb.AddField(field.Name, field.Value, field.Inline);
                }
            } 

            var m = await channel.SendMessageAsync(plaintext, embed: eb.Build());

            if (timeout is TimeSpan ts)
            {
                _ = Task.Run(async () => await Task.Delay(ts).ContinueWith(_ => m.DeleteAsync()));
            }

            return m;
        }

        public static async Task<IUserMessage> SendOkAsync(this IMessageChannel channel, string message, TimeSpan? timeout = null)
        {
            var m = await channel.SendMessageAsync(string.Empty,
                embed: new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription(message)
                    .Build());

            if (timeout is TimeSpan ts)
            {
                _ = Task.Run(async () => await Task.Delay(ts).ContinueWith(_ => m.DeleteAsync()));
            }

            return m;
        }

        public static async Task<IUserMessage> SendErrorAsync(this IMessageChannel channel, string message, TimeSpan? timeout = null)
        {
            var m = await channel.SendMessageAsync(string.Empty,
                embed: new EmbedBuilder()
                    .WithErrorColor()
                    .WithDescription(message)
                    .Build());

            if (timeout is TimeSpan ts)
            {
                _ = Task.Run(async () => await Task.Delay(ts).ContinueWith(_ => m.DeleteAsync()));
            }

            return m;
        }

        public static async Task<IUserMessage> SendWarnAsync(this IMessageChannel channel, string message, TimeSpan? timeout = null)
        {
            var m = await channel.SendMessageAsync(string.Empty,
                embed: new EmbedBuilder()
                    .WithWarnColor()
                    .WithDescription(message)
                    .Build());

            if (timeout is TimeSpan ts)
            {
                _ = Task.Run(async () => await Task.Delay(ts).ContinueWith(_ => m.DeleteAsync()));
            }

            return m;
        }

        public static async Task<IUserMessage> SendWinAsync(this IMessageChannel channel, string message, TimeSpan? timeout = null)
        {
            var m = await channel.SendMessageAsync(string.Empty,
                embed: new EmbedBuilder()
                    .WithWinColor()
                    .WithDescription(message)
                    .Build());

            if (timeout is TimeSpan ts)
            {
                _ = Task.Run(async () => await Task.Delay(ts).ContinueWith(_ => m.DeleteAsync()));
            }

            return m;
        }

        public static async Task<IUserMessage> SendLoseAsync(this IMessageChannel channel, string message, TimeSpan? timeout = null)
        {
            var m = await channel.SendMessageAsync(string.Empty,
                embed: new EmbedBuilder()
                    .WithLoseColor()
                    .WithDescription(message)
                    .Build());

            if (timeout is TimeSpan ts)
            {
                _ = Task.Run(async () => await Task.Delay(ts).ContinueWith(_ => m.DeleteAsync()));
            }

            return m;
        }
    }
}
