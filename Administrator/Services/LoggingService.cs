using Administrator.Extensions;
using Administrator.Services.Database;
using Discord;
using Discord.WebSocket;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Administrator.Services.Database.Models;
using Discord.Commands;

namespace Administrator.Services
{
    public class LoggingService
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly List<IMessage> _toIgnore = new List<IMessage>();
        private readonly DbService _db;

        public LoggingService(BaseSocketClient client, DbService db, CommandService commands)
        {
            _db = db;

            client.UserJoined += OnUserJoined;
            client.MessageDeleted += OnMessageDeleted;
            client.UserBanned += OnUserBanned;
            client.UserUnbanned += OnUserUnbanned;
            client.UserLeft += OnUserLeft;
            client.Log += LogMessage;
            commands.Log += LogMessage;
        }

        public void AddIgnoredMessages(IEnumerable<IMessage> messages)
        {
            _toIgnore.AddRange(messages);
        }

        private Task LogMessage(LogMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.Message)) return Task.CompletedTask;

            switch (message.Severity)
            {
                case LogSeverity.Critical:
                    Log.Fatal(message.Message);
                    if (message.Exception is Exception ex1)
                        Log.Fatal(ex1, ex1.ToString);
                    break;
                case LogSeverity.Error:
                    Log.Error(message.Message);
                    if (message.Exception is Exception ex2)
                        Log.Error(ex2, ex2.ToString);
                    break;
                case LogSeverity.Warning:
                    Log.Warn(message.Message);
                    if (message.Exception is Exception ex3)
                        Log.Warn(ex3, ex3.ToString);
                    break;
                case LogSeverity.Info:
                    Log.Info(message.Message);
                    break;
                case LogSeverity.Verbose:
                    break;
                case LogSeverity.Debug:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return Task.CompletedTask;
        }

        /*
        private Task OnUserUnbanned(SocketUser user, SocketGuild guild)
        {
            var _ = InternalUserUnbanned(user, guild);
            return Task.CompletedTask;
        }
        */

        private async Task OnUserUnbanned(SocketUser user, SocketGuild guild)
        {
            //Log.ConditionalDebug($"User unbanned: {user} [{user.Id}]");

            var guildConfig = await _db.GetOrCreateGuildConfigAsync(guild).ConfigureAwait(false);
            {
                if (guild.TryGetChannelId(guildConfig.LogChannel.ToString(), out var channelId))
                {
                    var channel = guild.GetChannel(channelId) as ISocketMessageChannel;
                    var eb = new EmbedBuilder()
                        .WithOkColor()
                        .WithThumbnailUrl(user.AvatarUrl())
                        .WithTitle("User unbanned")
                        .WithDescription($"{user}")
                        .AddField("ID", user.Id)
                        .WithCurrentTimestamp();

                    await channel.EmbedAsync(eb.Build());
                }
            }
        }

        /*
        private Task OnUserLeft(SocketGuildUser user)
        {
            var _ = InternalUserLeft(user);
            return Task.CompletedTask;
        }
        */

        private async Task OnUserLeft(SocketGuildUser user)
        {
            //Log.ConditionalDebug($"User left: {user} [{user.Id}]");

            var guildConfig = await _db.GetOrCreateGuildConfigAsync(user.Guild).ConfigureAwait(false);
            {
                if (user.Guild.TryGetChannelId(guildConfig.LogChannel.ToString(), out var channelId))
                {
                    var channel = user.Guild.GetChannel(channelId) as ISocketMessageChannel;
                    var eb = new EmbedBuilder()
                        .WithErrorColor()
                        .WithThumbnailUrl(user.AvatarUrl())
                        .WithTitle("User left")
                        .WithDescription($"{user}")
                        .AddField("ID", user.Id)
                        .WithCurrentTimestamp();

                    await channel.EmbedAsync(eb.Build());
                }
            }
            var userPhrases = await _db.GetAsync<UserPhrase>(x => x.UserId == (long) user.Id && x.GuildId == (long) user.Guild.Id).ConfigureAwait(false);

            if (userPhrases.FirstOrDefault() is UserPhrase up)
            {
                var phrases = await _db.GetAsync<Phrase>(x => x.GuildId == (long) user.Guild.Id && (x.UserId == (long) user.Id || x.UserPhraseId == up.Id))
                    .ConfigureAwait(false);
                await _db.DeleteAsync(up).ConfigureAwait(false);
                await _db.DeleteAllExceptAsync<Phrase>(x => !phrases.Contains(x)).ConfigureAwait(false);
            }
            else
            {
                var phrases = await _db
                    .GetAsync<Phrase>(x => x.UserId == (long) user.Id && x.GuildId == (long) user.Guild.Id)
                    .ConfigureAwait(false);

                await _db.DeleteAllExceptAsync<Phrase>(x => !phrases.Contains(x)).ConfigureAwait(false);
            }
        }

        /*
        private Task OnUserBanned(SocketUser user, SocketGuild guild)
        {
            var _ = InternalUserBanned(user, guild);
            return Task.CompletedTask;
        }
        */

        private async Task OnUserBanned(SocketUser user, SocketGuild guild)
        {
            //Log.ConditionalDebug($"User banned: {user} [{user.Id}]");
            var guildConfig = await _db.GetOrCreateGuildConfigAsync(guild).ConfigureAwait(false);
            {
                if (guild.TryGetChannelId(guildConfig.LogChannel.ToString(), out var channelId))
                {
                    var channel = guild.GetChannel(channelId) as ISocketMessageChannel;
                    var eb = new EmbedBuilder()
                        .WithErrorColor()
                        .WithThumbnailUrl(user.AvatarUrl())
                        .WithTitle("User banned")
                        .WithDescription($"{user}")
                        .AddField("ID", user.Id)
                        .WithCurrentTimestamp();

                    await channel.EmbedAsync(eb.Build());
                }
            }

            var channels = guild.TextChannels;
            var count = 0;
            foreach (var c in channels)
                try
                {
                    var msgs = await c.GetMessagesAsync(200).FlattenAsync().ConfigureAwait(false);
                    msgs = msgs.Where(m => m.Author.Equals(user));
                    AddIgnoredMessages(msgs.ToList());
                    count += msgs.Count();
                }
                catch
                {
                    Log.Warn("Could not get or add messages to ignore.");
                }

            Log.ConditionalDebug($"Added {count} messages to ignore for this user.");
        }

        /*
        private Task OnMessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            var _ = InternalMessageDeleted(message, channel);
            return Task.CompletedTask;
        }
        */

        private async Task OnMessageDeleted(Cacheable<IMessage, ulong> msg, ISocketMessageChannel channel)
        {
            //Log.ConditionalDebug("Message deleted.");
            var message = await msg.GetOrDownloadAsync().ConfigureAwait(false);
            if (message.Author.IsBot || _toIgnore.Select(x => x.Id).Contains(message.Id)) return;
            if (!(channel is SocketGuildChannel chnl)) return;
            var guildConfig = await _db.GetOrCreateGuildConfigAsync(chnl.Guild).ConfigureAwait(false);

            if (chnl.Guild.TryGetChannelId(guildConfig.LogChannel.ToString(),
                    out var channelId)
                && chnl.Guild.GetChannel(channelId) is ISocketMessageChannel chan)
            {
                var eb = new EmbedBuilder()
                    .WithErrorColor()
                    .WithTitle($"Message deleted in #{channel.Name}")
                    .WithDescription(message.Author.ToString());
                if (!string.IsNullOrWhiteSpace(message.Content))
                    eb.AddField("Content", message.Content);
                eb.AddField("ID", message.Id)
                    .WithCurrentTimestamp();

                if (message.Content.TryExtractUri(out var updated, out var uri)
                    && uri.ToString().ToLower().EndsWith("png", "jpg", "bmp", "jpeg"))
                {
                    eb.WithImageUrl(uri.ToString());
                }
                else if (message.Attachments.Any())
                {
                    var attachment = message.Attachments.First();
                    if (attachment.Url.ToLower().EndsWith("png", "jpg", "bmp", "jpeg"))
                        eb.WithImageUrl(attachment.Url);
                }

                await chan.EmbedAsync(eb.Build());
            }

            var suggestions = await _db.GetAsync<Suggestion>(x => x.MessageId == (long) message.Id).ConfigureAwait(false);
            if (suggestions.FirstOrDefault() is Suggestion s)
                await _db.DeleteAsync(s).ConfigureAwait(false);

            var rrms = await _db.GetAsync<ReactionRoleMessage>(x => x.Id == (long) message.Id).ConfigureAwait(false);
            if (rrms.FirstOrDefault() is ReactionRoleMessage rrm)
                await _db.DeleteAsync(rrm).ConfigureAwait(false);
        }

        /*
        private Task OnUserJoined(SocketGuildUser user)
        {
            var _ = InternalUserJoined(user);
            return Task.CompletedTask;
        }
        */

        private async Task OnUserJoined(SocketGuildUser user)
        {
            //Log.ConditionalDebug($"User joined: {user} [{user.Id}]");
            var guildConfig = await _db.GetOrCreateGuildConfigAsync(user.Guild).ConfigureAwait(false);

            if (user.Guild.TryGetChannelId(guildConfig.LogChannel.ToString(), out var channelId))
            {
                var channel = user.Guild.GetChannel(channelId) as ISocketMessageChannel;
                var eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithThumbnailUrl(user.AvatarUrl())
                    .WithTitle("User joined")
                    .WithDescription($"{user}")
                    .AddField("ID:", user.Id)
                    .AddField("Joined Discord:", $"{user.CreatedAt:g} GMT")
                    .WithCurrentTimestamp();

                await channel.EmbedAsync(eb.Build());
            }

            if (user.Guild.TryGetChannelId(guildConfig.GreetChannel.ToString(), out var welcomeChannelId)
                && guildConfig.GreetUserOnJoin)
            {
                var channel = user.Guild.GetChannel(welcomeChannelId) as ISocketMessageChannel;
                var eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle($"Welcome to {user.Guild.Name}, {user}!")
                    .WithThumbnailUrl(user.AvatarUrl())
                    .WithDescription(guildConfig.GreetMessage.Replace("{user}", user.Mention));
                        //"Please be sure to carefully read over our rules, and feel free to add a reaction to the message above to add some class roles to yourself!\nEnjoy your stay!");

                var _ = channel.SendMessageAsync(guildConfig.MentionUserOnJoin ? user.Mention : string.Empty, TimeSpan.FromSeconds(guildConfig.GreetTimeout), embed: eb.Build()).ConfigureAwait(false);
            }
        }
    }
}