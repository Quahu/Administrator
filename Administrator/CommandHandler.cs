using Administrator.Common;
using Administrator.Common.Database;
using Administrator.Extensions;
using Administrator.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Administrator
{
    public static class CommandHandler
    {
        private const string INVITE_PATTERN = @"discord(?:\.com|\.gg)[\/invite\/]?(?:(?!.*[Ii10OolL]).[a-zA-Z0-9]{5,6}|[a-zA-Z0-9\-]{2,32})";
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static IServiceProvider _services;

        public static void Initialize(IServiceProvider services)
        {
            _services = services;
            _services.GetService<CommandService>().CommandExecuted += OnCommandExecuted;
            _services.GetService<CommandService>().Log += LogErrorAsync;
            Log.Info("Initialized.");
        }

        private static async Task LogErrorAsync(LogMessage msg)
        {
            if (msg.Exception is CommandException ex
                && ex.Command.RunMode == RunMode.Async
                && ex.Context.Guild is SocketGuild guild)
            {
                Log.Error(ex.InnerException, ex.InnerException.ToString);
                using (var scope = _services.CreateScope())
                using (var ctx = scope.ServiceProvider.GetService<AdminContext>())
                {
                    var gc = ctx.GetOrCreateGuildConfig(guild);
                    if (gc.VerboseErrors == Functionality.Enable)
                    {
                        await ex.Context.Channel.SendErrorAsync(ex.Message);
                    }
                }
            }
        }

        public static async Task HandleAsync(SocketMessage message)
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

        public static async Task<bool> TryFilterMessageAsync(SocketMessage msg)
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
                    && Regex.IsMatch(msg.Content, INVITE_PATTERN))
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

        private static async Task OnCommandExecuted(CommandInfo command, ICommandContext context,
            IResult result)
        {
            if (result.IsSuccess && result is AdminResult sr)
            {
                Log.ConditionalDebug(result.GetType());
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
    }
}
