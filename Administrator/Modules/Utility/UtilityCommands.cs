using Administrator.Common;
using Administrator.Extensions;
using Administrator.Extensions.Attributes;
using Administrator.Modules.Utility.Services;
using Administrator.Services;
using Administrator.Services.Database;
using Administrator.Services.Database.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using MoreLinq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Administrator.Modules.Utility
{
    internal enum SuggestionModification
    {
        Approve,
        Deny
    }

    [Name("Utility")]
    public class UtilityCommands : ModuleBase<SocketCommandContext>
    {
        private static readonly Config Config = BotConfig.New();
        private readonly CommandService _commands;
        private readonly DbService _db;
        private readonly LoggingService _logging;
        private readonly SuggestionService _suggestions;
        private readonly StatsService _stats;

        public UtilityCommands(DbService db, SuggestionService suggestions,
            CommandService commands, LoggingService logging, StatsService stats)
        {
            _db = db;
            _suggestions = suggestions;
            _commands = commands;
            _logging = logging;
            _stats = stats;
        }

        #region Looking To Play
        
        [Command("lookingtoplay")]
        [Alias("ltp")]
        [Summary("Toggle \"Looking to Play\" status on yourself. This role is mentionable and lasts as long as many hours you specify (default 2), up to the guild's maximum allowed time." +
               "\nTo remove the role from yourself early, simply use the command again.")]
        [Usage("{p}ltp")]
        [RequireContext(ContextType.Guild)]
        private async Task ToggleLtpAsync(long hours = 2)
        {
            var eb = new EmbedBuilder();
            var ltpUsers = await _db.GetAsync<LtpUser>(x => x.GuildId == (long) Context.Guild.Id).ConfigureAwait(false);
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (hours > gc.LookingToPlayMaxHours && gc.LookingToPlayMaxHours != default)
            {
                var _ = Context.Channel.SendErrorAsync($"Please enter a number {gc.LookingToPlayMaxHours} or smaller.")
                    .ConfigureAwait(false);
            }
            else if (hours < 1 && gc.LookingToPlayMaxHours != default)
            {
                hours = gc.LookingToPlayMaxHours;
            }
            if (!(Context.Guild.Roles.FirstOrDefault(r => r.Id == (ulong) gc.LookingToPlayRole) is SocketRole ltpRole))
            {
                var _ = Context.Channel.SendErrorAsync("Seems the Looking to Play role hasn't been set up. Try asking a member with the bot's perm role.", TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
            else if (ltpUsers.FirstOrDefault(u => u.UserId == (long) Context.User.Id && u.GuildId == (long) Context.Guild.Id) is LtpUser ltpUser)
            {
                eb.WithOkColor()
                    .WithDescription(
                        $"**{Context.User}** has successfully removed the {ltpRole.Name} role.");
                await _db.DeleteAsync(ltpUser).ConfigureAwait(false);
                var _ = Context.Channel.EmbedAsync(eb.Build(), TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                await (Context.User as SocketGuildUser).RemoveRoleAsync(ltpRole).ConfigureAwait(false);
            }
            else
            {
                await (Context.User as SocketGuildUser).AddRoleAsync(ltpRole).ConfigureAwait(false);
                eb.WithOkColor()
                    .WithDescription($"**{Context.User}** has successfully added the {ltpRole.Name} role.\n(This role will automatically be removed after {hours} hour(s).)")
                    .AddField($"Current Members of \"{ltpRole.Name}\":", $"```css\n{string.Join(", ", ltpRole.Members.Select(l => l.ToString())).Replace($"{Context.User},", string.Empty)}, {Context.User}\n```");
                await Context.Channel.SendMessageAsync(gc.MentionLtpUsers ? ltpRole.Mention : string.Empty, embed: eb.Build()).ConfigureAwait(false);

                var ltp = new LtpUser
                {
                    UserId = (long) Context.User.Id,
                    GuildId = (long) Context.Guild.Id,
                    Expires = hours < 1 ? DateTimeOffset.MaxValue : DateTimeOffset.UtcNow + TimeSpan.FromHours(hours)
                };

                await _db.InsertAsync(ltp).ConfigureAwait(false);
            }
        }

        #endregion

        #region General

        [Command("ping")]
        [Summary("Check the bot's API latency.")]
        [Usage("{p}ping")]
        [RequirePermissionsPass]
        private async Task PingAsync()
            => await Context.Channel.SendConfirmAsync($"🏓 Pong! Current API latency is {Context.Client.Latency}")
                .ConfigureAwait(false);

        [Command("stats")]
        [Summary("Get bot stats, including uptime, guilds, and other information.")]
        [Usage("{p}stats")]
        [RequirePermissionsPass]
        private async Task GetStatsAsync()
        {
            var app = await Context.Client.GetApplicationInfoAsync().ConfigureAwait(false);
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithAuthor(new EmbedAuthorBuilder
                {
                    IconUrl = Context.Client.CurrentUser.GetAvatarUrl(),
                    Name = $"{Context.Client.CurrentUser.Username} {_stats.BotVersion}"
                })
                .WithFooter($"Created by {app.Owner}")
                .AddField("Uptime",
                    $"{_stats.Uptime.Days} days\n" +
                    $"{_stats.Uptime.Hours} hours\n" +
                    $"{_stats.Uptime.Minutes} minutes\n" +
                    $"{_stats.Uptime.Seconds} seconds", true)
                .AddField("Presence",
                    $"Guilds: {_stats.Guilds}\n" +
                    $"Text channels: {_stats.TextChannels}\n" +
                    $"Voice channels: {_stats.VoiceChannels}\n" +
                    $"Total members: {_stats.Users}", true)
                .AddField("Commands run", _stats.CommandsRun, true)
                .AddField("Messages received", $"{_stats.MessagesReceived} ({_stats.MessagesReceived / _stats.Uptime.TotalSeconds:F} / second)", true);

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        [Command("getinvite")]
        [Alias("invite", "inv")]
        [Summary("Get the server invite.")]
        [Remarks("To set the invite, use {p}invitecode.")]
        [Usage("{p}invite")]
        [RequireContext(ContextType.Guild)]
        private async Task GetInviteAsync()
        {
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(gc.InviteCode)) return;
            
            await Context.Channel.SendMessageAsync($"https://discord.gg/{gc.InviteCode}").ConfigureAwait(false);
        }

        [Command("emotes")]
        [Alias("em")]
        [Summary("Show the guild's custom emotes.")]
        [Usage("{p}emotes")]
        [RequireContext(ContextType.Guild)]
        [RequirePermissionsPass]
        private async Task GetEmojisAsync()
        {
            var emotes = Context.Guild.Emotes.ToList();
            if (!emotes.Any())
            {
                await Context.Channel.SendErrorAsync("No emotes found on this guild.").ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Server Emotes");

            if (string.Join(string.Empty, emotes).Length > 2048)
            {
                var count = emotes.Count / 2;
                eb.AddField("🌟", string.Join(string.Empty, emotes.Take(count)))
                    .AddField("🌟", string.Join(string.Empty, emotes.Skip(count)));
            }
            else
            {
                eb.WithDescription(string.Join("", emotes));
            }

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        
        [Command("avatar")]
        [Alias("av")]
        [Summary("Get a user's avatar. Defaults to yourself.")]
        [Usage("{p}av", "{p}av @SomeUser")]
        [RequireContext(ContextType.Guild)]
        [RequirePermissionsPass]
        //[Ratelimit(1, 0.5, Measure.Minutes)]
        private async Task GetAvatarAsync(IUser user = null)
        {
            var u = user ?? Context.User;
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle($"Avatar for {u}")
                .WithImageUrl($"{u.GetAvatarUrl()}?size=512");
            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        #endregion

        #region Help

        [Command("help")]
        [Alias("h")]
        [Summary("Get DMed a help message, or get help for an individual command.")]
        [Usage("{p}h {p}phrase", "{p}help")]
        [Priority(0)]
        private async Task GetHelpAsync()
        {
            var app = await Context.Client.GetApplicationInfoAsync().ConfigureAwait(false);
            var modules = _commands.Modules.Where(x => !x.Name.Equals("BotOwner")).ToList();
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithAuthor($"Hello! I am {Context.Client.CurrentUser.Username}.",
                    Context.Client.CurrentUser.GetAvatarUrl())
                .WithDescription(
                    $"I'm a multipurpose bot created by {app.Owner} for the /r/tf2 Discord, gone global!\n" +
                    "I feature loads of commands and features, but alas I am still a work in progress.")
                .AddField("🗒 Command List", "For a detailed command list, check out [this link.](https://github.com/QuantumToasted/Administrator/wiki/Command-List)")
                .AddField($"{Emote.Parse("<:TFDiscord:445038772858388480>")} Invite me!", $"To add me to your server, follow [this link](https://discordapp.com/api/oauth2/authorize?client_id={Context.Client.CurrentUser.Id}&permissions=1543892214&scope=bot) and select the guild you'd like to add me to!")
                .AddField("❓ Quick Help", $"Available modules:\n```{string.Join(", ", modules.Select(x => x.Name))}\n```" +
                                        $"\nTo get the modules listed above from anywhere, use `{Config.BotPrefix}modules`.\n" +
                                        $"To get a list of commands for that module, use `{Config.BotPrefix}commands ModuleName`.\neg. `{Config.BotPrefix}commands Utility`\n\n" +
                                        $"To get help for an __individual__ command, use `{Config.BotPrefix}help {Config.BotPrefix}commandName`. The `{Config.BotPrefix}` is not required for the command name you are searching for.")
                .AddField("🔍 Browse the source!", "Want to see what makes me tick? Feel free to check out my [GitHub page.](https://github.com/QuantumToasted/Administrator)\n(You should also make feature requests or bug reports on the Issues page!)")
                .AddField("🎙 Additional support",
                    "Can't find what you're looking for? Feel free to join the [help guild.](https://discord.gg/rTvGube)");

            try
            {
                var dm = await Context.User.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                await dm.EmbedAsync(eb.Build()).ConfigureAwait(false);
                if (Context.Channel is IPrivateChannel) return;
                await Context.Message.AddReactionAsync(new Emoji("\U00002705")).ConfigureAwait(false);
            }
            catch
            {
                await Context.Message.AddReactionAsync(new Emoji("\U00002753")).ConfigureAwait(false);
            }
        }

        [Command("help")]
        [Alias("h")]
        [Summary("Get DMed a help message, or get help for an individual command.")]
        [Usage("{p}h {p}phrase", "{p}help")]
        private async Task GetHelpAsync(string cmd)
        {
            // !!h !!BAN
            // !!h ban
            cmd = cmd.ToLower().TrimStart(Config.BotPrefix.ToArray());
            var eb = new EmbedBuilder();

            if (_commands.Commands.FirstOrDefault(x => x.Name.Equals(cmd) || x.Aliases.Contains(cmd)) is CommandInfo
                command)
            {
                eb.WithOkColor()
                    .WithTitle($"{{p}}{string.Join(" / {p}", command.Aliases)}".Replace("{p}", Config.BotPrefix))
                    .WithDescription($"{command.Summary}\n");

                if (!string.IsNullOrEmpty(command.Remarks)) eb.WithFooter(command.Remarks.Replace("{p}", Config.BotPrefix));

                if (command.Preconditions.FirstOrDefault(x => x is RequireBotPermissionAttribute)
                    is RequireBotPermissionAttribute botPerms && botPerms.GuildPermission is GuildPermission g)
                {
                    eb.Description += $"\nRequires **{string.Join("**, **", g.GetFlags())}** bot permissions.";
                }

                if (command.Preconditions.FirstOrDefault(x => x is RequireUserPermissionAttribute) is
                    RequireUserPermissionAttribute userPerms && userPerms.GuildPermission is GuildPermission gp)
                {
                    eb.Description += $"\nRequires **{string.Join("**, **", gp.GetFlags())}** user permissions.";
                }

                if (command.Preconditions.FirstOrDefault(x => x is RequirePermRole) is RequirePermRole rp)
                {
                    eb.Description += "\nRequires the guild's **PermRole** to use.";
                }

                if (command.Attributes.FirstOrDefault(x => x is UsageAttribute) is UsageAttribute usage)
                {
                    eb.AddField("Usage", $"`{string.Join("` or `", usage.Text)}`".Replace("{p}", Config.BotPrefix));
                }

                await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
            }
            else
            {
                await Context.Channel.SendErrorAsync("Couldn't find info about that command.").ConfigureAwait(false);
            }
        }

        [Command("modules")]
        [Summary("Get a list of available modules.")]
        [Usage("{p}modules")]
        private async Task GetModulesAsync()
        {
            var modules = _commands.Modules.Where(x => !x.Name.Equals("BotOwner")).ToList();
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Available modules:")
                .WithDescription($"```\n{string.Join("\n", modules.Select(x => x.Name))}\n```");

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        [Command("commands")]
        [Alias("cmds")]
        [Summary("Get a list of commands for a specific module.")]
        [Usage("{p}commands Utility")]
        private async Task GetCommandsAsync([Remainder] string moduleName = null)
        {
            if (moduleName is null)
            {
                await Context.Channel
                    .SendErrorAsync(
                        $"You must supply a module name! Use `{Config.BotPrefix}modules` for a list of modules.")
                    .ConfigureAwait(false);
            }

            if (_commands.Modules.FirstOrDefault(x =>
                !x.Name.Equals("BotOwner", StringComparison.InvariantCultureIgnoreCase)
                && x.Name.Equals(moduleName, StringComparison.InvariantCultureIgnoreCase)) is ModuleInfo module)
            {
                var description = "```css\n";
                foreach (var command in module.Commands.DistinctBy(x => x.Name).ToList())
                {
                    description += $"{Config.BotPrefix}{command.Name}";
                    foreach (var a in command.Aliases.Where(x => !x.Equals(command.Name)).ToList())
                    {
                        description += $" [{Config.BotPrefix}{a}]";
                    }

                    description += "\n";
                }

                description += "\n```";

                var eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle($"Commands for module {module.Name}:")
                    .WithDescription(description);

                await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
            }
        }

        #endregion

        #region Suggestions

        [Command("suggest")]
        [Summary("Make a suggestion. Add an image link anywhere to have it automatically embedded.")]
        [Usage("{p}suggest Ban all mods.", "{p}suggest Make this the server icon: https://i.imgur.com/vnQEfqA.jpg")]
        [RequireContext(ContextType.Guild)]
        [RequirePermissionsPass]
        private async Task SuggestAsync([Remainder] string suggestion)
        {
            if (string.IsNullOrWhiteSpace(suggestion)) return;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithAuthor(Context.Message.Author);

            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);
            var suggestions = await _db.GetAsync<Suggestion>().ConfigureAwait(false);

            if (Context.Message.Attachments.Any())
            {
                var attachment = Context.Message.Attachments.First();
                if (attachment.Url.ToLower().EndsWith("png", "jpg", "bmp", "jpeg")) eb.WithImageUrl(attachment.Url);
                eb.WithDescription(suggestion);
            }
            else if (suggestion.TryExtractUri(out var updated, out var uri))
            {
                eb.WithImageUrl(uri.ToString())
                    .WithDescription(updated);
            }
            else
            {
                eb.WithDescription(suggestion);
            }
            
            if (suggestions.OrderByDescending(x => x.Id).FirstOrDefault() is Suggestion s)
            {
                eb.WithFooter($"Suggestion ID: {s.Id + 1}");
            }
            else
            {
                eb.WithFooter($"Suggestion ID: 1");
            }

            var channel = Context.Guild.TextChannels.FirstOrDefault(x => x.Id == (ulong) gc.SuggestionChannel)
                          ?? Context.Channel;

            var msg = await channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
            await _suggestions.AddNewAsync(msg, Context.User as SocketGuildUser).ConfigureAwait(false);
            _logging.AddIgnoredMessages(new List<IMessage> {msg});
            await Context.Message.DeleteAsync().ConfigureAwait(false);

            var dm = await Context.Message.Author.GetOrCreateDMChannelAsync().ConfigureAwait(false);
            try
            {
                var eb2 = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Thanks for posting a suggestion!")
                    .WithDescription(
                        "Made it by mistake or screwed up your wording or spelling? Delete it by reacting with :wastebasket: (\\:wastebasket\\:)!");

                await dm.SendMessageAsync(string.Empty, embed: eb2.Build());
            }
            catch
            {
                // ignored
            }
        }

        [Command("suggestions")]
        [Summary("Display a list of suggestions. The default sort order is **newest**.")]
        [Usage("{p}suggestions top", "{p}suggestions")]
        [Remarks("Current options are: Newest, Oldest, Top, Bottom, Best.")]
        [RequireContext(ContextType.Guild)]
        [RequirePermissionsPass]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [Priority(1)]
        private async Task GetSuggestionsAsync(int page = 1)
        {
            await GetSuggestionsAsync("newest", page).ConfigureAwait(false);
        }

        [Command("suggestions")]
        [Summary("Display a list of suggestions. The default sort order is **newest**.")]
        [Usage("{p}suggestions top", "{p}suggestions")]
        [Remarks("Current options are: Newest, Oldest, Top, Bottom, Best.")]
        [RequireContext(ContextType.Guild)]
        [RequirePermissionsPass]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [Priority(0)]
        private async Task GetSuggestionsAsync(string sort = "newest", int page = 1)
        {
            var suggestionList = await _db.GetAsync<Suggestion>(x => x.GuildId == (long) Context.Guild.Id).ConfigureAwait(false);

            if (suggestionList.Any())
            {
                var guild = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);
                var eb = new EmbedBuilder()
                    .WithOkColor();
                sort = sort.ToLower();

                if (sort.Equals("newest"))
                    suggestionList = suggestionList.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * 10).Take(10)
                        .ToList();
                else if (sort.Equals("oldest"))
                    suggestionList = suggestionList.OrderBy(x => x.CreatedAt).Skip((page - 1) * 10).Take(10).ToList();
                else if (sort.Equals("top"))
                    suggestionList = suggestionList.OrderByDescending(x => x.Upvotes).Skip((page - 1) * 10).Take(10)
                        .ToList();
                else if (sort.Equals("bottom"))
                    suggestionList = suggestionList.OrderByDescending(x => x.Downvotes).Skip((page - 1) * 10).Take(10)
                        .ToList();
                else if (sort.Equals("best"))
                    suggestionList = suggestionList.OrderByDescending(x => x.Upvotes - x.Downvotes)
                        .Skip((page - 1) * 10).Take(10).ToList();

                suggestionList = suggestionList.Take(10).ToList();
                sort = sort[0].ToString().ToUpper() + sort.Substring(1);
                eb.WithTitle($"{sort} {suggestionList.Count} suggestion(s):")
                    .WithDescription("▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬");
                foreach (var s in suggestionList)
                {
                    var id = $"ID: {s.Id}";
                    var votes = $"{DiscordExtensions.GetEmote(guild.UpvoteArrow)}{s.Upvotes} {DiscordExtensions.GetEmote(guild.DownvoteArrow)}{s.Downvotes}";
                    var author = $"{Context.Guild.GetUser((ulong) s.UserId)}";
                    var timestamp = $"{s.CreatedAt:d}";
                    var text = $"{s.Content}\n{s.ImageUrl}\n▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬";
                    eb.AddField(string.Join(" | ", votes, id, author, timestamp), text);
                }

                await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
            }
        }

        [Command("suggestion")]
        [Summary("Approve or deny a suggestion by message ID, or show a suggestion by ID.")]
        [Usage("{p}suggestion approve 1234567890", "{p}suggestion deny 1234567890", "{p}suggestion 5")]
        [RequireContext(ContextType.Guild)]
        [RequirePermissionsPass]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [Priority(1)]
        private async Task ModifySuggestionAsync(long id)
        {
            var guild = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);
            var suggestionList = await _db.GetAsync<Suggestion>(x => x.Id == id && x.GuildId == (long) Context.Guild.Id).ConfigureAwait(false);

            if (suggestionList.FirstOrDefault() is Suggestion s)
            {
                var eb = new EmbedBuilder()
                    .WithOkColor();
                var msgId = $"ID: {s.Id}";
                var votes = $"{DiscordExtensions.GetEmote(guild.UpvoteArrow)}{s.Upvotes} {DiscordExtensions.GetEmote(guild.DownvoteArrow)}{s.Downvotes}";
                var author = $"{Context.Guild.GetUser((ulong) s.UserId)}";
                var timestamp = $"{s.CreatedAt:d}";
                var text = $"{s.Content}\n{s.ImageUrl}\n▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬";
                eb.AddField(string.Join(" | ", votes, msgId, author, timestamp), text);

                await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
            }
        }

        [Command("suggestion")]
        [Summary("Approve or deny a suggestion by message ID, or show the `x` next suggestion.")]
        [Usage("{p}suggestion approve 1234567890", "{p}suggestion deny 1234567890", "{p}suggestion 5")]
        [RequireContext(ContextType.Guild)]
        [RequirePermissionsPass]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [Priority(0)]
        private async Task ModifySuggestionAsync(string option, long id)
        {
            if (Enum.TryParse(option.ToUpperFirst(), out SuggestionModification mod))
            {
                var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);
                var suggestionList = await _db.GetAsync<Suggestion>(x => x.GuildId == (long) Context.Guild.Id && x.Id == id)
                    .ConfigureAwait(false);

                if (suggestionList.FirstOrDefault() is Suggestion s
                    && Context.Guild.TextChannels.FirstOrDefault(x =>
                            x.Id == (ulong) gc.SuggestionChannel) is SocketTextChannel suggestionChannel
                    && Context.Guild.TextChannels.FirstOrDefault(x =>
                            x.Id == (ulong) gc.SuggestionArchive) is SocketTextChannel suggestionArchive)
                {
                    var eb = new EmbedBuilder();
                    var msg = await suggestionChannel.GetMessageAsync((ulong) s.MessageId).ConfigureAwait(false);

                    switch (mod)
                    {
                        case SuggestionModification.Approve:
                            eb.WithOkColor()
                                .WithAuthor(new EmbedAuthorBuilder
                                {
                                    IconUrl = "https://i.imgur.com/yOBMdyi.png",
                                    Name =
                                        $"Suggestion approved with {s.Upvotes} upvotes and {s.Downvotes} downvotes."
                                })
                                .WithTitle(Context.Guild.GetUser((ulong) s.UserId).ToString())
                                .WithThumbnailUrl(Context.Guild.GetUser((ulong) s.UserId).GetAvatarUrl())
                                .WithDescription(s.Content);
                            if (msg.Embeds.FirstOrDefault() is Embed e
                                && e.Image.GetValueOrDefault() is EmbedImage ei)
                            {
                                eb.WithImageUrl(ei.Url);
                            }
                            break;
                        case SuggestionModification.Deny:
                            eb.WithErrorColor()
                                .WithAuthor(new EmbedAuthorBuilder
                                {
                                    IconUrl = "https://i.imgur.com/GI0eqJU.png",
                                    Name =
                                        $"Suggestion denied with {s.Upvotes} upvotes and {s.Downvotes} downvotes."
                                })
                                .WithTitle(Context.Guild.GetUser((ulong) s.UserId).ToString())
                                .WithThumbnailUrl(Context.Guild.GetUser((ulong) s.UserId).GetAvatarUrl())
                                .WithDescription(s.Content)
                                .WithImageUrl(msg.Embeds.First().Image.GetValueOrDefault().Url);
                            break;
                    }

                    await Context.Message.AddReactionAsync(new Emoji("\U00002705")).ConfigureAwait(false);
                    await suggestionArchive.EmbedAsync(eb.Build()).ConfigureAwait(false);
                    await _suggestions.RemoveAsync(msg as IUserMessage).ConfigureAwait(false);
                    await msg.DeleteAsync().ConfigureAwait(false);
                }
            }
            else
            {
                await Context.Message.AddReactionAsync(new Emoji("\U00002753")).ConfigureAwait(false);
            }
            /*
            var guildCfg = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);
            var suggestionList = await _db.GetSuggestionsAsync().ConfigureAwait(false);
            var archiveChannel = Context.Guild.GetChannel((ulong) guildCfg.SuggestionArchive) as ISocketMessageChannel;
            var suggestionChanneal =
                Context.Guild.GetChannel((ulong) guildCfg.SuggestionChannel) as ISocketMessageChannel;
            var currentSuggestions = await suggestionChannel.GetMessagesAsync().FlattenAsync().ConfigureAwait(false);

            if (suggestionList.FirstOrDefault(s => s.MessageId == (long) messageId) is Suggestion s
                && currentSuggestions.FirstOrDefault(m => m.Id == messageId) is IUserMessage msg)
                if (option.Equals("approve", StringComparison.InvariantCultureIgnoreCase))
                {
                    eb.WithOkColor()
                        .WithAuthor(new EmbedAuthorBuilder
                        {
                            IconUrl = "https://i.imgur.com/yOBMdyi.png",
                            Name =
                                $"Suggestion approved with {s.Upvotes} upvotes and {s.Downvotes} downvotes."
                        })
                        .WithTitle(Context.Guild.GetUser((ulong) s.UserId).ToString())
                        .WithThumbnailUrl(Context.Guild.GetUser((ulong) s.UserId).AvatarUrl())
                        .WithDescription(s.Content)
                        .WithImageUrl(msg.Embeds.First().Image.GetValueOrDefault().Url);
                    await Context.Message.AddReactionAsync(new Emoji("\U00002705")).ConfigureAwait(false);
                    await archiveChannel.EmbedAsync(eb.Build()).ConfigureAwait(false);
                    await _suggestions.RemoveAsync(msg).ConfigureAwait(false);
                    await msg.DeleteAsync().ConfigureAwait(false);
                }
                else if (option.Equals("deny", StringComparison.InvariantCultureIgnoreCase))
                {
                    eb.WithErrorColor()
                        .WithAuthor(new EmbedAuthorBuilder
                        {
                            IconUrl = "https://i.imgur.com/GI0eqJU.png",
                            Name =
                                $"Suggestion denied with {s.Upvotes} upvotes and {s.Downvotes} downvotes."
                        })
                        .WithTitle(Context.Guild.GetUser((ulong) s.UserId).ToString())
                        .WithThumbnailUrl(Context.Guild.GetUser((ulong) s.UserId).AvatarUrl())
                        .WithDescription(s.Content)
                        .WithImageUrl(msg.Embeds.First().Image.GetValueOrDefault().Url);
                    await Context.Message.AddReactionAsync(new Emoji("\U00002705")).ConfigureAwait(false);
                    await archiveChannel.EmbedAsync(eb.Build()).ConfigureAwait(false);
                    await _suggestions.RemoveAsync(msg).ConfigureAwait(false);
                    await msg.DeleteAsync().ConfigureAwait(false);
                }
                else
                {
                    await Context.Message.AddReactionAsync(new Emoji("\U00002753")).ConfigureAwait(false);
                }
                */
        }

        #endregion
    }
}