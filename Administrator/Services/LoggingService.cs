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
using Administrator.Common;
using Discord.Rest;

namespace Administrator.Services
{
    public class LoggingService
    {
        private static readonly Config Config = BotConfig.New();
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly List<IMessage> _toIgnore = new List<IMessage>();
        private readonly DbService _db;
        private readonly DiscordSocketClient _client;

        public LoggingService(DiscordSocketClient client, DbService db, CommandService commands)
        {
            _db = db;
            _client = client;

            _client.UserJoined += OnUserJoined;
            _client.MessageDeleted += OnMessageDeleted;
            _client.UserBanned += OnUserBanned;
            _client.UserUnbanned += OnUserUnbanned;
            _client.UserLeft += OnUserLeft;
            _client.Log += LogMessage;
            _client.MessageUpdated += OnMessageUpdated;
            _client.JoinedGuild += OnGuildJoin;
            commands.Log += LogMessage;
        }

        private async Task OnGuildJoin(SocketGuild guild)
        {
            var gc = await _db.GetOrCreateGuildConfigAsync(guild).ConfigureAwait(false);
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithThumbnailUrl(_client.CurrentUser.AvatarUrl())
                .WithTitle("Thanks for inviting me!")
                .WithDescription(
                    $"I am the Administrator. I'm an all-purpose bot with some neat features, dare I say. If you would like some help to see what I can do, utilize the `{Config.BotPrefix}help` command.");

            if (guild.Roles.All(x => x.Id != (ulong) gc.PermRole))
            {
                try
                {
                    var role = guild.Roles.FirstOrDefault(x => x.Name.Equals($"{guild.Name} Permrole")) ??
                                       (IRole) await guild.CreateRoleAsync($"{guild.Name} Permrole", GuildPermissions.None)
                                           .ConfigureAwait(false);
                    gc.PermRole = (long) role.Id;
                    await _db.UpdateAsync(gc).ConfigureAwait(false);
                    eb.Description +=
                        $"\nTo use my commands which require my permrole, give yourself or other admins the **{role.Name}** role.";
                }
                catch
                {
                    eb.AddField("⚠ Sorry, but I could not automatically create a permrole for you to use.",
                        "I may not have the proper permissions. I require **ManageRoles** perms to be able to perform tasks like assigning the mute role and looking to play role(s).\n" +
                        $"You will have to manually set it using `{Config.BotPrefix}permrole`.\nSee `{Config.BotPrefix}help permrole` for info on how to set it.");
                }
            }

            if (guild.TextChannels
                .Where(c => guild.CurrentUser.GetPermissions(c).SendMessages)
                .OrderBy(c => c.Position).FirstOrDefault() is SocketTextChannel ch)
            {
                await ch.EmbedAsync(eb.Build()).ConfigureAwait(false);
            }
        }

        private async Task OnMessageUpdated(Cacheable<IMessage, ulong> oldMessage, SocketMessage newMessage, ISocketMessageChannel channel)
        {
            if (!(newMessage is SocketUserMessage msg)
                || !(msg.Channel is SocketGuildChannel chnl)) return;

            var blws = await _db.GetAsync<BlacklistedWord>(x => x.GuildId == (long) chnl.Guild.Id)
                .ConfigureAwait(false);

            if (blws.Any(x => msg.Content.ToLower().Contains(x.Word.ToLower())))
            {
                var gc = await _db.GetOrCreateGuildConfigAsync(chnl.Guild).ConfigureAwait(false);
                if (msg.Author is SocketGuildUser u && u.Roles.All(x => x.Id != (ulong) gc.PermRole))
                {
                    try
                    {
                        await msg.DeleteAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
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
                && guildConfig.GreetUserOnJoin
                && user.Guild.GetChannel(welcomeChannelId) is ISocketMessageChannel chnl)
            {
                try
                {
                    var a = TomlEmbedBuilder.ReadToml(guildConfig.GreetMessage.Replace("{user}", user.Mention));
                    if (a is TomlEmbed e)
                    {
                        var _1 = chnl.EmbedAsync(e, TimeSpan.FromSeconds(guildConfig.GreetTimeout)).ConfigureAwait(false);
                    }
                }
                catch
                {
                    var _2 = chnl.SendMessageAsync(guildConfig.GreetMessage.Replace("{user}", user.Mention), TimeSpan.FromSeconds(guildConfig.GreetTimeout)).ConfigureAwait(false);
                }
            }
        }
    }
}