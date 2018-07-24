using Administrator.Common;
using Administrator.Common.Database;
using Administrator.Common.Database.Models;
using Administrator.Extensions;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Imgur.API.Authentication.Impl;
using Imgur.API.Endpoints.Impl;
using Microsoft.Extensions.DependencyInjection;
using Nett;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NLog.Config;

namespace Administrator.Services
{
    public static class HandlerService
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static readonly List<IUserMessage> IgnoredMessages = new List<IUserMessage>();
        private static IServiceProvider _services;

        public static void Initialize(IServiceProvider services)
        {
            _services = services;
            _services.GetRequiredService<DiscordSocketClient>().Ready += HandleReady;
            _services.GetRequiredService<DiscordSocketClient>().MessageReceived += HandleMessageReceivedAsync;
            _services.GetRequiredService<DiscordSocketClient>().MessageDeleted += HandleMessageDeletionAsync;
            _services.GetRequiredService<DiscordSocketClient>().UserBanned += HandleUserBannedAsync;
            _services.GetRequiredService<DiscordSocketClient>().UserLeft += HandleUserLeftAsync;
            _services.GetRequiredService<DiscordSocketClient>().UserJoined += HandleUserJoinedAsync;
            _services.GetRequiredService<DiscordSocketClient>().UserUnbanned += HandleUserUnbannedAsync;
            _services.GetRequiredService<DiscordSocketClient>().MessageUpdated += HandleMessageUpdatedAsync;
            _services.GetRequiredService<DiscordSocketClient>().Log += LogClientMessage;
            _services.GetRequiredService<CommandService>().CommandExecuted += HandleCommandExecutedAsync;
            _services.GetRequiredService<CommandService>().Log += LogErrorAsync;
            LogManager.Configuration.AddTarget(new NLog.Targets.ColoredConsoleTarget("Discord"));
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

        private static Task LogClientMessage(LogMessage msg)
        {
            if (string.IsNullOrWhiteSpace(msg.Message)) return Task.CompletedTask;
           
            var discordLog = LogManager.GetLogger("Discord");

            switch (msg.Severity)
            {
                case LogSeverity.Critical:
                    discordLog.Fatal(msg.Message);
                    if (msg.Exception is Exception critEx)
                        Log.Fatal(critEx, critEx.Message);
                    break;
                case LogSeverity.Error:
                    discordLog.Error(msg.Message);
                    if (msg.Exception is Exception errEx)
                        Log.Error(errEx, errEx.Message);
                    break;
                case LogSeverity.Warning:
                    discordLog.Warn(msg.Message);
                    if (msg.Exception is Exception warnEx)
                        Log.Warn(warnEx, warnEx.Message);
                    break;
                case LogSeverity.Info:
                    discordLog.Info(msg.Message);
                    break;
                case LogSeverity.Debug:
                case LogSeverity.Verbose:
                    break;
            }

            return Task.CompletedTask;
        }

        private static async Task HandleMessageUpdatedAsync(Cacheable<IMessage, ulong> oldMessage, SocketMessage newMessage, ISocketMessageChannel channel)
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

                if (await TryFilterMessageAsync(newMsg)) return;
                
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

        private static async Task HandleUserUnbannedAsync(SocketUser user, SocketGuild guild)
        {
            using (var scope = _services.CreateScope())
            using (var ctx = scope.ServiceProvider.GetService<AdminContext>())
            {
                var gc = ctx.GetOrCreateGuildConfig(guild);
                if (!(guild.GetTextChannel(gc.LogUnbanChannelId) is SocketTextChannel logChannel)) return;

                if (!(ctx.Infractions.OfType<Ban>()
                    .Where(x => x.GuildId == gc.Id)
                        .OrderByDescending(x => x.Id)
                        .FirstOrDefault(x => x.ReceiverId == user.Id && !x.HasBeenRevoked) is Ban ban))
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

        private static async Task HandleUserJoinedAsync(SocketGuildUser user)
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

                if (ctx.Infractions.OfType<Mute>().Where(x => x.GuildId == gc.Id).Any(x => x.ReceiverId == user.Id && !x.HasExpired && !x.HasBeenRevoked)
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

        private static async Task HandleUserLeftAsync(SocketGuildUser user)
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

        private static async Task HandleUserBannedAsync(SocketUser user, SocketGuild guild)
        {
            using (var scope = _services.CreateScope())
            using (var ctx = scope.ServiceProvider.GetService<AdminContext>())
            {
                var gc = ctx.GetOrCreateGuildConfig(guild);
                if (!(guild.GetTextChannel(gc.LogBanChannelId) is SocketTextChannel logChannel)) return;

                if (!(ctx.Infractions.OfType<Ban>()
                        .Where(x => x.GuildId == gc.Id)
                        .OrderByDescending(x => x.Id)
                        .FirstOrDefault(x => x.ReceiverId == user.Id) is Ban ban))
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

        private static async Task HandleMessageDeletionAsync(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
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

        private static async Task LogErrorAsync(LogMessage msg)
        {
            if (msg.Exception is CommandException ex
                && ex.Command.RunMode == RunMode.Async
                && ex.Context.Guild is SocketGuild guild)
            {
                Log.Error(ex, ex.ToString);
                using (var scope = _services.CreateScope())
                using (var ctx = scope.ServiceProvider.GetService<AdminContext>())
                {
                    var gc = ctx.GetOrCreateGuildConfig(guild);
                    if (gc.VerboseErrors == Functionality.Enable)
                    {
                        await ex.Context.Channel.SendErrorAsync(ex.InnerException.Message);
                    }
                }
            }
        }

        private static async Task HandleMessageReceivedAsync(SocketMessage message)
        {
            if (!(message is SocketUserMessage msg)
                || await TryFilterMessageAsync(msg)
                || msg.Author.IsBot) return;

            var commands = _services.GetService<CommandService>();
            var context = new SocketCommandContext(_services.GetService<DiscordSocketClient>(), msg);
            var argPos = 0;

            StatsService.IncrementMessagesReceived(context.Guild);

            using (var scope = _services.CreateScope())
            using (var ctx = scope.ServiceProvider.GetService<AdminContext>())
            {
                if (msg.Content.Equals($"{BotConfig.Prefix}prefix", StringComparison.OrdinalIgnoreCase))
                {
                    if (context.Channel is IPrivateChannel)
                    {
                        await context.Channel.SendOkAsync($"Default prefix is \"{BotConfig.Prefix}\".");
                        return;
                    }

                    await context.Channel.SendOkAsync($"Prefix on this guild is \"{ctx.GetPrefixOrDefault(context.Guild)}\".");
                    return;
                }

                if (!msg.HasStringPrefix(ctx.GetPrefixOrDefault(context.Guild), ref argPos))
                {
                    return;
                }

                var result = await commands.ExecuteAsync(context, argPos, scope.ServiceProvider);

                if (result.IsSuccess || result.Error == CommandError.UnknownCommand) return;
                switch (result)
                {
                    case AdminResult ar:
                        Log.Warn($"Command errored after {ar.ExecuteTime.TotalSeconds:F}s\n" +
                                 $"\t\tGuild: {context.Guild?.Name} [{context.Guild?.Id}]\n" +
                                 $"\t\tChannel: {context.Channel.Name} [{context.Channel.Id}]\n" +
                                 $"\t\tUser: {context.User} [{context.User.Id}]\n" +
                                 $"\t\tMessage: {context.Message.Content.Replace("\n", string.Empty)}\n" +
                                 $"\t\tReason: {ar.Reason}");
                        switch (ar.Error)
                        {
                            case CommandError.Unsuccessful:
                                if (!string.IsNullOrWhiteSpace(ar.Message) && ar.Embed is null)
                                {
                                    await context.Channel.SendErrorAsync(ar.Message);
                                }
                                else if (ar.Embed is Embed e)
                                {
                                    await context.Channel.SendMessageAsync(ar.Message ?? string.Empty, embed: e);
                                }
                                else goto default;
                                break;
                            default:
                                await TrySendVerboseErrorAsync(context, ctx, ar);
                                break;
                        }
                        break;
                    default:
                        Log.Warn("Command errored\n" +
                                 $"\t\tGuild: {context.Guild?.Name} [{context.Guild?.Id}]\n" +
                                 $"\t\tChannel: {context.Channel.Name} [{context.Channel.Id}]\n" +
                                 $"\t\tUser: {context.User} [{context.User.Id}]\n" +
                                 $"\t\tMessage: {context.Message.Content.Replace("\n", string.Empty)}\n" +
                                 $"\t\tReason: {result.ErrorReason}");
                        await TrySendVerboseErrorAsync(context, ctx, result);
                        break;
                }
            }
        }

        private static async Task<bool> TryFilterMessageAsync(SocketMessage msg)
        {
            if (!(msg.Author is SocketGuildUser u)) return false;

            using (var scope = _services.CreateScope())
            using (var ctx = scope.ServiceProvider.GetService<AdminContext>())
            {
                var gc = ctx.GetOrCreateGuildConfig(u.Guild);

                // filter blacklisted words
                if (ctx.MessageFilters?.Any(x => x.GuildId == gc.Id && x.IsMatch(msg.Content)) == true)
                {
                    if (u.Roles.All(x => x.Id != gc.PermRoleId))
                    {
                        await msg.TryDeleteAsync();
                    }
                }

                // filter invites
                if (u.GuildPermissions.ManageGuild
                    && gc.FilterInvites == Functionality.Enable
                    && Regex.IsMatch(msg.Content, BotConfig.INVITE_REGEX_PATTERN))
                {
                    var invites = await u.Guild.GetInvitesAsync();

                    if (invites.All(x => !msg.Content.Contains(x.Code)))
                    {
                        await msg.TryDeleteAsync();
                    }
                }
            }

            return false;
        }

        private static async Task HandleCommandExecutedAsync(CommandInfo command, ICommandContext context,
            IResult result)
        {
            if (result.IsSuccess && result is AdminResult sr)
            {
                Log.Info($"Command [{command.Name}] executed after {sr.ExecuteTime.TotalSeconds:F}s\n" +
                         $"\t\tGuild: {context.Guild?.Name} [{context.Guild?.Id}]\n" +
                         $"\t\tChannel: {context.Channel.Name} [{context.Channel.Id}]\n" +
                         $"\t\tUser: {context.User} [{context.User.Id}]\n" +
                         $"\t\tMessage: {context.Message.Content.Replace("\n", string.Empty)}");

                if (!string.IsNullOrWhiteSpace(sr.Message) && sr.Embed is null)
                {
                    await context.Channel.SendOkAsync(sr.Message);
                }
                else if (sr.Embed is Embed e)
                {
                    await context.Channel.SendMessageAsync(sr.Message ?? string.Empty, embed: e);
                }

                StatsService.IncrementCommandsExecuted(context.Guild);
            }
        }

        private static async Task<bool> TrySendVerboseErrorAsync(SocketCommandContext context, AdminContext ctx, IResult result)
        {
            if (!(context.Guild is SocketGuild guild)) return false;
            var gc = ctx.GetOrCreateGuildConfig(guild);
            if (gc.VerboseErrors == Functionality.Disable) return false;
            await context.Channel.SendErrorAsync(result.ErrorReason);
            return true;
        }

        private static Task HandleReady()
        {
            SchedulerService.Initialize(_services);
            StatsService.Initialize(_services);
            return Task.CompletedTask;
        }
    }
}
