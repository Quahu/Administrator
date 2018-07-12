using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Administrator.Common;
using Administrator.Common.Database;
using Administrator.Common.Database.Models;
using Administrator.Extensions;
using Discord;
using Discord.WebSocket;
using Imgur.API.Authentication.Impl;
using Imgur.API.Endpoints.Impl;
using Microsoft.Extensions.DependencyInjection;
using Nett;
using NLog;

namespace Administrator.Services
{
    public static class LoggingService
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static readonly List<IUserMessage> IgnoredMessages = new List<IUserMessage>();
        private static IServiceProvider _services;

        public static void Initialize(IServiceProvider services)
        {
            _services = services;
            var client = _services.GetService<DiscordSocketClient>();
            client.MessageDeleted += OnMessageDeleted;
            client.UserBanned += OnUserBanned;
            client.UserLeft += OnUserLeft;
            client.UserJoined += OnUserJoined;
            client.UserUnbanned += OnUserUnbanned;
            client.MessageUpdated += OnMessageUpdated;
            client.Log += LogMessage;
            Log.Info("Initialized.");
        }

        public static void Ignore(params IUserMessage[] msgs)
        {
            IgnoredMessages.AddRange(msgs);
        }

        public static void Ignore(IEnumerable<IUserMessage> msgs)
        {
            IgnoredMessages.AddRange(msgs);
        }

        private static Task LogMessage(LogMessage msg)
        {
            if (string.IsNullOrWhiteSpace(msg.Message)) return Task.CompletedTask;

            switch (msg.Severity)
            {
                case LogSeverity.Critical:
                    Log.Fatal(msg.Message);
                    if (msg.Exception is Exception critEx)
                        Log.Fatal(critEx, critEx.Message);
                    break;
                case LogSeverity.Error:
                    Log.Error(msg.Message);
                    if (msg.Exception is Exception errEx)
                        Log.Error(errEx, errEx.Message);
                    break;
                case LogSeverity.Warning:
                    Log.Warn(msg.Message);
                    if (msg.Exception is Exception warnEx)
                        Log.Warn(warnEx, warnEx.Message);
                    break;
                case LogSeverity.Info:
                    Log.Info(msg.Message);
                    break;
                case LogSeverity.Debug:
                case LogSeverity.Verbose:
                    break;
            }

            return Task.CompletedTask;
        }

        private static async Task OnMessageUpdated(Cacheable<IMessage, ulong> oldMessage, SocketMessage newMessage, ISocketMessageChannel channel)
        {
            if (!(newMessage is SocketUserMessage newMsg)
                || !(channel is SocketTextChannel chnl)
                || newMessage.Author.IsBot) return;
            var oldMsg = await oldMessage.GetOrDownloadAsync();

            if (newMsg.Content.Equals(oldMsg.Content)) return;

            using (var scope = _services.CreateScope())
            using (var ctx = scope.ServiceProvider.GetService<AdminContext>())
            {
                var gc = ctx.GetOrCreateGuildConfig(chnl.Guild);
                if (!(chnl.Guild.GetTextChannel(gc.LogMessageUpdatedChannelId) is SocketTextChannel logChannel)) return;

                if (await CommandHandler.TryFilterMessageAsync(newMsg)) return;
                
                await logChannel.EmbedAsync(new EmbedBuilder()
                    .WithWarnColor()
                    .WithTitle($"Message updated in #{chnl.Name}")
                    .WithDescription(newMsg.Author.ToString())
                    .AddField("ID", newMsg.Id, true)
                    .AddField("Original message", $"{oldMsg.Content}\n{oldMsg.Attachments.FirstOrDefault()?.Url}")
                    .AddField("New message", $"{newMsg.Content}\n{newMsg.Attachments.FirstOrDefault()?.Url}")
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build());
            }
        }

        private static async Task OnUserUnbanned(SocketUser user, SocketGuild guild)
        {
            using (var scope = _services.CreateScope())
            using (var ctx = scope.ServiceProvider.GetService<AdminContext>())
            {
                var gc = ctx.GetOrCreateGuildConfig(guild);
                if (!(guild.GetTextChannel(gc.LogUnbanChannelId) is SocketTextChannel logChannel)) return;

                if (!(ctx.Infractions.OfType<Ban>()
                    .Where(x => x.GuildId == gc.Id)
                        .OrderByDescending(x => x.Id)
                        .FirstOrDefault(x => x.ReceieverId == user.Id && !x.HasBeenRevoked) is Ban ban))
                {
                    await logChannel.EmbedAsync(new EmbedBuilder()
                        .WithWarnColor()
                        .WithTitle("User unbanned")
                        .WithDescription(user.ToString())
                        .AddField("ID", user.Id)
                        .WithThumbnailUrl(user.GetAvatarUrl())
                        .WithTimestamp(DateTimeOffset.UtcNow)
                        .Build());
                    return;
                }

                await logChannel.EmbedAsync(new EmbedBuilder()
                    .WithWarnColor()
                    .WithTitle($"Ban - Case #{ban.Id}")
                    .WithDescription(
                        $"User **{user}** (`{user.Id}`) has been unbanned.")
                    .WithModerator(guild.GetUser(ban.RevokerId))
                    .WithTimestamp(ban.RevocationTimestamp)
                    .Build());
            }
        }

        private static async Task OnUserJoined(SocketGuildUser user)
        {
            using (var scope = _services.CreateScope())
            using (var ctx = scope.ServiceProvider.GetService<AdminContext>())
            {
                var gc = ctx.GetOrCreateGuildConfig(user.Guild);
                if (!(user.Guild.GetTextChannel(gc.LogJoinChannelId) is SocketTextChannel logChannel)) return;

                await logChannel.EmbedAsync(new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("User joined")
                    .WithDescription(user.ToString())
                    .AddField("Mention", user.Mention, true)
                    .AddField("ID", user.Id, true)
                    .AddField("Joined Discord", $"{user.CreatedAt:g} UTC", true)
                    .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build());

                if (gc.GreetChannelId == 0
                    || !(user.Guild.GetTextChannel(gc.GreetChannelId) is SocketTextChannel greetChannel)
                    || string.IsNullOrWhiteSpace(gc.GreetMessage)) return;

                try
                {
                    var toml = Toml.ReadString<TomlEmbed>(gc.GreetMessage
                        .Replace("{user}", user.Mention)
                        .Replace("{user.full}", user.ToString())
                        .Replace("{user.id}", user.Id.ToString())
                        .Replace("{user.name}", user.Username)
                        .Replace("{user.discrim}", user.Discriminator)
                        .Replace("{guild}", user.Guild.Name)
                        .Replace("{guild.id}", user.Guild.Id.ToString()));
                    await greetChannel.EmbedAsync(toml, gc.GreetTimeout);
                }
                catch
                {
                    await greetChannel.SendMessageAsync(gc.GreetMessage
                        .Replace("{user}", user.Mention)
                        .Replace("{user.full}", user.ToString())
                        .Replace("{user.id}", user.Id.ToString())
                        .Replace("{user.name}", user.Username)
                        .Replace("{user.discrim}", user.Discriminator)
                        .Replace("{guild}", user.Guild.Name)
                        .Replace("{guild.id}", user.Guild.Id.ToString()), gc.GreetTimeout);
                }

                if (ctx.Infractions.OfType<Mute>().Where(x => x.GuildId == gc.Id).Any(x => x.ReceieverId == user.Id && !x.HasExpired && !x.HasBeenRevoked)
                    && user.Guild.GetRole(gc.MuteRoleId) is SocketRole muteRole)
                {
                    try
                    {
                        await user.AddRoleAsync(muteRole);
                    }
                    catch
                    {
                        Log.Warn($"User {user} joined {user.Guild.Name}, but I could not restore their mute.");
                    }
                }
            }
        }

        private static async Task OnUserLeft(SocketGuildUser user)
        {
            using (var scope = _services.CreateScope())
            using (var ctx = scope.ServiceProvider.GetService<AdminContext>())
            {
                var gc = ctx.GetOrCreateGuildConfig(user.Guild);
                if (!(user.Guild.GetTextChannel(gc.LogJoinChannelId) is SocketTextChannel logChannel)) return;

                await logChannel.EmbedAsync(new EmbedBuilder()
                    .WithErrorColor()
                    .WithTitle("User left")
                    .WithDescription(user.ToString())
                    .AddField("ID", user.Id, true)
                    .WithThumbnailUrl(user.GetAvatarUrl())
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build());
            }
        }

        private static async Task OnUserBanned(SocketUser user, SocketGuild guild)
        {
            using (var scope = _services.CreateScope())
            using (var ctx = scope.ServiceProvider.GetService<AdminContext>())
            {
                var gc = ctx.GetOrCreateGuildConfig(guild);
                if (!(guild.GetTextChannel(gc.LogBanChannelId) is SocketTextChannel logChannel)) return;

                if (!(ctx.Infractions.OfType<Ban>()
                        .Where(x => x.GuildId == gc.Id)
                        .OrderByDescending(x => x.Id)
                        .FirstOrDefault(x => x.ReceieverId == user.Id) is Ban ban))
                {
                    var b = (await guild.GetBansAsync()).FirstOrDefault(x => x.User.Id == user.Id);
                    
                    await logChannel.EmbedAsync(new EmbedBuilder()
                        .WithErrorColor()
                        .WithTitle("User banned")
                        .WithDescription(user.ToString())
                        .AddField("ID", user.Id)
                        .AddField("Reason", b?.Reason ?? "-")
                        .WithThumbnailUrl(user.GetAvatarUrl())
                        .WithTimestamp(DateTimeOffset.UtcNow)
                        .Build());
                    return;
                }

                await logChannel.EmbedAsync(new EmbedBuilder()
                    .WithErrorColor()
                    .WithTitle($"Ban - Case #{ban.Id}")
                    .WithDescription(
                        $"User **{user}** (`{user.Id}`) has been banned.")
                    .AddField("Reason", ban.Reason)
                    .WithModerator(guild.GetUser(ban.IssuerId))
                    .WithTimestamp(ban.Timestamp)
                    .Build());
            }
        }

        private static async Task OnMessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            var msg = await message.GetOrDownloadAsync();
            if (msg.Author.IsBot) return;
            string url = null;

            try
            {
                if (msg.Attachments.FirstOrDefault(x => x.Url.IsImageUrl()) is Attachment a)
                {
                    var stream = await _services.GetService<HttpClient>().GetStreamAsync(a.Url);
                    var endpoint = new ImageEndpoint(_services.GetService<ImgurClient>());
                    var img = await endpoint.UploadImageStreamAsync(stream);
                    url = img.Link;
                }
            }
            catch
            {
                // ignored
            }
            
            
            using (var scope = _services.CreateScope())
            using (var ctx = scope.ServiceProvider.GetService<AdminContext>())
            {
                if (!(channel is SocketTextChannel chnl)) return;
                var gc = ctx.GetOrCreateGuildConfig(chnl.Guild);
                if (!(chnl.Guild.GetTextChannel(gc.LogMessageUpdatedChannelId) is SocketTextChannel logChannel)) return;

                await logChannel.EmbedAsync(new EmbedBuilder()
                    .WithErrorColor()
                    .WithTitle($"Message deleted in #{chnl.Name}")
                    .WithDescription(msg.Author.ToString())
                    .AddField("Content", $"{msg.Content}\n{msg.Attachments.FirstOrDefault()?.Url}")
                    .AddField("ID", msg.Id)
                    .WithImageUrl(url)
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build());
            }
        }
    }
}
