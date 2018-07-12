using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Administrator.Common;
using Administrator.Common.Attributes;
using Administrator.Extensions;
using Administrator.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using MoreLinq;

namespace Administrator.Modules.Info
{
    [Name("Info")]
    [RequirePermissionsPass]
    public class InfoCommands : AdminBase
    {
        [Command("userinfo")]
        [Alias("uinfo")]
        [Summary("Gets a user's info, including roles, join date, and other info. Defaults to yourself.")]
        [Usage("uinfo", "uinfo @SomeUser")]
        [RequireContext(ContextType.Guild)]
        private Task<RuntimeResult> GetUserInfoAsync(SocketGuildUser user = null)
        {
            user = user ?? Context.User as SocketGuildUser;
            if (user is null) return CommandError("User not found.");

            var roles = user.Roles.Where(x => x.Id != Context.Guild.EveryoneRole.Id).ToList();
            return CommandSuccess(embed: new EmbedBuilder()
                .WithHighestRoleColor(user)
                .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .WithTitle($"User info for {user}")
                .AddField("Mention", user.Mention, true)
                .AddField("Nickname", user.Nickname ?? "N/A", true)
                .AddField("ID", user.Id, true)
                .AddField("Joined server", user.JoinedAt is DateTimeOffset dto ? $"{dto:g} UTC\n({StringExtensions.CreateOrdinal(Context.Guild.Users.OrderBy(x => x.JoinedAt).ToList().IndexOf(user) + 1)} out of {Context.Guild.MemberCount} users)" : "N/A", true)
                .AddField("Joined Discord", $"{user.CreatedAt:g} UTC", true)
                .AddField("Roles", roles.Any() ? string.Join("\n", roles.Select(x => x.Name)) : "None")
                .Build());
        }

        [Command("guildinfo")]
        [Alias("ginfo")]
        [Summary("Get's the guild's information, including owner, creation date, and other information.")]
        [Usage("ginfo")]
        [RequireContext(ContextType.Guild)]
        private Task<RuntimeResult> GetGuildInfoAsync()
        {
            return CommandSuccess(embed: new EmbedBuilder()
                .WithOkColor()
                .WithTitle($"Guild info for {Context.Guild.Name}")
                .WithThumbnailUrl(Context.Guild.IconUrl)
                .AddField("Creation date", $"{Context.Guild.CreatedAt:g} UTC", true)
                .AddField("ID", Context.Guild.Id, true)
                .AddField("Stats", $"Members: {Context.Guild.MemberCount}\n" +
                                   $"Bots: {Context.Guild.Users.Count(x => x.IsBot)}\n" +
                                   $"Text Channels: {Context.Guild.TextChannels.Count}\n" +
                                   $"Voice Channnels: {Context.Guild.VoiceChannels.Count}\n" +
                                   $"Categories: {Context.Guild.CategoryChannels.Count}", true)
                .AddField("Messages received", $"{StatsService.GetMessagesReceived(Context.Guild)} ({StatsService.GetMessagesReceived(Context.Guild) / StatsService.Uptime.TotalSeconds:F} / second)", true)
                .AddField("Command executed", StatsService.GetCommandsExecuted(Context.Guild) + 1, true)
                .Build());
        }

        [Command("ping")]
        [Summary("Checks the bot's Discord API latency.")]
        [Usage("ping")]
        private Task<RuntimeResult> PingAsync()
            => CommandSuccess($"🏓 Pong! Gateway API latency is currently {Context.Client.Latency}ms.");

        [Command("help")]
        [Alias("h")]
        [Summary("Have the bot DM you a help message, or get help for a specific command.")]
        [Usage("help", "help commandName")]
        private async Task<RuntimeResult> GetHelpAsync()
        {
            try
            {
                var app = await Context.Client.GetApplicationInfoAsync();
                var dm = await Context.User.GetOrCreateDMChannelAsync();
                await dm.EmbedAsync(new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Hello! I am the Administrator.")
                    .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl())
                    .WithDescription($"I was originally designed as a multipurpose bot for the /r/tf2 Discord server by {app.Owner}.\n" +
                                     "I have since gone through a huge overhaul and am now operating on a global scale!\n" +
                                     "I feature loads of commands and features, but alas I am still a work in progress.")
                    .AddField("🗒 Command list", "Check out [the official command list](https://github.com/QuantumToasted/Administrator/wiki/Command-List) for a complete list of commands and how to use them.")
                    .AddField($"{Emote.Parse("<:TFDiscord:445038772858388480>")} Invite me!", $"To add me to your server, follow [the invite link](https://discordapp.com/oauth2/authorize?client_id={Context.Client.CurrentUser.Id}&permissions=1543892214&scope=bot) and select the guild you'd like to add me to!")
                    .AddField("❓ Quick help",
                        "Available modules:\n" +
                        $"```\n{string.Join(", ", Commands.Modules.Where(x => !x.IsSubmodule).Select(x => x.Name))}\n```\n" +
                        $"To get the modules listed above from anywhere, use `{BotConfig.Prefix}modules`.\n" +
                        $"To get a list of commands for a module, use `{BotConfig.Prefix}commands ModuleName`.\n" +
                        $"eg. `{BotConfig.Prefix}commands Utility`\n\n" +
                        $"To get help for an __individual__ command, use `{BotConfig.Prefix}help commandName`.")
                    .AddField("🔍 Browse the source!",
                        "Want to see what makes me tick? Feel free to check out my [GitHub page.](https://github.com/QuantumToasted/Administrator)\n" +
                        "(You should also make feature requests or bug reports on the Issues page!)")
                    .AddField("🎙 Additional support", "Can't find what you're looking for? Feel free to join the [help guild.](https://discord.gg/rTvGube)")
                    .Build());
                if (!Context.IsPrivate) await AddCheckAsync();
                return await CommandSuccess();
            }
            catch
            {
                return await CommandError("Could not send a DM to this user.");
            }
        }

        [Command("help")]
        [Alias("h")]
        [Summary("Have the bot DM you a help message, or get help for a specific command.")]
        [Usage("help", "help commandName")]
        private Task<RuntimeResult> GetHelpAsync(string commandName)
        {
            var regex = new Regex(Regex.Escape(DbContext.GetPrefixOrDefault(Context.Guild)));
            commandName = regex.Replace(commandName, string.Empty, 1);
            if (!(Commands.Commands.FirstOrDefault(x =>
                    x.Aliases.Any(y =>
                        y.Equals(commandName, StringComparison.OrdinalIgnoreCase)))
                is CommandInfo command))
            {
                return CommandError("Command not found.",
                    "Could not find info for that command.");
            }

            var s = string.Empty;
            if (command.Preconditions.Any(x => x is RequireBotOwnerAttribute))
            {
                s += "\nRequires **BotOwner** permissions.";
            }

            if (command.Preconditions.Any(x => x is RequirePermRoleAttribute))
            {
                s += "\nRequires the bot's **PermRole**.";
            }

            if (command.Preconditions.FirstOrDefault(x => x is RequireUserPermissionAttribute) is
                RequireUserPermissionAttribute rupa && rupa.GuildPermission is GuildPermission ugp)
            {
                s += $"\nRequires **{ugp:F}** user permission(s).";
            }

            if (command.Preconditions.FirstOrDefault(x => x is RequireBotPermissionAttribute) is
                    RequireBotPermissionAttribute rbpa && rbpa.GuildPermission is GuildPermission bgp)
            {
                s += $"\nRequires **{bgp:F}** bot permission(s).";
            }

            var summary = command.Summary.Replace("{prefix}", DbContext.GetPrefixOrDefault(Context.Guild))
                .Replace("{defaultprefix}", BotConfig.Prefix);
            var remarks = command.Remarks?.Replace("{prefix}", DbContext.GetPrefixOrDefault(Context.Guild))
                .Replace("{defaultprefix}", BotConfig.Prefix)
                .Replace("{prefixmaxlength}", BotConfig.PREFIX_MAX_LENGTH.ToString())
                .Replace("{phraseminlength}", BotConfig.PHRASE_MIN_LENGTH.ToString());
            var usage = string.Join(" or ",
                (command.Attributes.First(x => x is UsageAttribute) as UsageAttribute)?.Text.Select(x =>
                    $"`{DbContext.GetPrefixOrDefault(Context.Guild)}{x}`"));

            return CommandSuccess(embed: new EmbedBuilder()
                .WithOkColor()
                .WithTitle(string.Join(" / ", command.Aliases.Select(x => $"{DbContext.GetPrefixOrDefault(Context.Guild)}{x}")))
                .WithDescription($"{summary}\n{s}")
                .AddField("Usage", usage)
                .WithFooter(remarks)
                .Build());
        }

        [Command("modules")]
        [Summary("Get a list of module names currently available for use.")]
        [Usage("modules")]
        private Task<RuntimeResult> GetModules()
            => CommandSuccess(embed: new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Available modules")
                .WithDescription($"```\n{string.Join(", ", Commands.Modules.Where(x => !x.IsSubmodule).Select(x => x.Name))}\n```")
                .WithFooter($"Use `{DbContext.GetPrefixOrDefault(Context.Guild)}commands moduleName` to get a list of commands for that module.")
                .Build());

        [Command("commands")]
        [Alias("cmds")]
        [Summary("Get a list of command names and aliases for a specific module.")]
        [Usage("commands Utility")]
        private Task<RuntimeResult> GetCommandsForModule([Remainder] string moduleName)
        {
            if (Commands.Modules.Any(x => x.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase)))
            {
                var modules = Commands.Modules.Where(x => x.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                return CommandSuccess(embed: new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle($"Commands for module {modules[0].Name}")
                    .WithDescription($"```css\n{string.Join("\n", modules.SelectMany(x => x.Commands).DistinctBy(x => x.Name).Select(x => $"{DbContext.GetPrefixOrDefault(Context.Guild)}{x.Name} {(x.Aliases.Count > 1 ? "\n\t" + string.Join("\n\t", x.Aliases.Skip(1).Select(y => $"[{DbContext.GetPrefixOrDefault(Context.Guild)}{y}]")) : string.Empty)}"))}\n```")
                    .Build());
            }

            return CommandError("Module not found.", "No module was found by that name.");
        }


        [Command("stats")]
        [Summary("Get bot stats, including total presence, commands run, and memory usage.")]
        [Usage("stats")]
        private async Task<RuntimeResult> GetStatsAsync()
        {
            var owner = await StatsService.GetOwnerAsync();
            return await CommandSuccess(embed: new EmbedBuilder()
                .WithOkColor()
                .WithAuthor($"{Context.Client.CurrentUser.Username} version {StatsService.BOT_VERSION}", Context.Client.CurrentUser.GetAvatarUrl())
                .AddField("Uptime", $"{StatsService.Uptime.Days} days\n{StatsService.Uptime.Hours} hours\n{StatsService.Uptime.Minutes} minutes\n{StatsService.Uptime.Seconds} seconds", true)
                .AddField("Presence", $"Guilds: {StatsService.TotalGuilds}\nText channels: {StatsService.TotalTextChannels}\nVoice channels: {StatsService.TotalVoiceChannels}\nTotal members: {StatsService.TotalUsers}", true)
                .AddField("Commands run", StatsService.CommandsExecuted.Sum(x => x.Value), true)
                .AddField("Messages receieved", $"{StatsService.MessagesReceived.Sum(x => x.Value)} ({StatsService.MessagesReceived.Sum(x => x.Value) / StatsService.Uptime.TotalSeconds:F} / second)", true)
                .AddField("Memory usage", $"{StatsService.GetTotalMemoryUsage() / 1000000D:F}MB", true)
                .WithFooter($"Created by {owner}", owner.GetAvatarUrl())
                .Build());
        }
    }
}
