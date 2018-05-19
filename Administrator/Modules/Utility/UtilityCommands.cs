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
        [Summary("Toggle \"Looking to Play\" status on yourself. This role is mentionable and lasts as long as you specify, up to and defaulting to the guild's maximum allowed time." +
               "\nTo remove the role from yourself early, simply use the command again.")]
        [Usage("{p}ltp")]
        [RequireContext(ContextType.Guild)]
        private async Task ToggleLtpAsync(long hours = 0)
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

        [Command("getadmin")]
        [Summary("Get a link to invite the bot to your server.")]
        [Usage("{p}getadmin")]
        private async Task GetAdminAsync()
        {
            try
            {
                var dm = await Context.User.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                await dm.SendMessageAsync(
                        $"Use this link to invite me to your server! https://discordapp.com/api/oauth2/authorize?client_id={Context.Client.CurrentUser.Id}&permissions=1543892214&scope=bot")
                    .ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        }

        [Command("stats")]
        [Summary("Get bot stats, including uptime, guilds, and other information.")]
        [Usage("{p}stats")]
        private async Task GetStatsAsync()
        {
            var app = await Context.Client.GetApplicationInfoAsync().ConfigureAwait(false);
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithAuthor(new EmbedAuthorBuilder
                {
                    IconUrl = Context.Client.CurrentUser.AvatarUrl(),
                    Name = $"{Context.Client.CurrentUser.Username} {_stats.BotVersion}"
                })
                .WithFooter($"Created by {app.Owner}")
                .AddField("Uptime",
                    $"{_stats.Uptime.Days} days\n" +
                    $"{_stats.Uptime.Hours} hours\n" +
                    $"{_stats.Uptime.Minutes} minutes\n" +
                    $"{_stats.Uptime.Seconds} seconds", true)
                .AddField("Presence",
                    $"Servers: {_stats.Guilds}\n" +
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
                .WithImageUrl($"{u.AvatarUrl()}?size=512");
            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        #endregion

        #region Help

        [Command("help")]
        [Alias("h")]
        [Summary("Get DMed a list of commands or get help for an individual command.")]
        [Usage("{p}h {p}phrase", "{p}h")]
        [Priority(0)]
        [RequirePermissionsPass]
        private async Task GetHelpAsync()
        {
            var modules = _commands.Modules.ToList();
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Command list")
                .WithFooter("Use {p}h {p}command to see help for a specific command.".Replace("{p}", Config.BotPrefix));

            if (!Config.OwnerIds.Contains((long) Context.Message.Author.Id))
                modules = modules.Where(x => !x.Name.Equals("BotOwner")).ToList();

            foreach (var module in modules)
            {
                var description = "```css\n";

                foreach (var cmd in module.Commands.DistinctBy(x => x.Name))
                {
                    var cmdName = cmd.Aliases.First();
                    var aliasesStr = string.Empty;
                    var cmdAliases = cmd.Aliases.Skip(1).ToList();
                    if (cmdAliases.Count > 0)
                        aliasesStr = $" [{Config.BotPrefix}{string.Join($"] [{Config.BotPrefix}", cmdAliases)}]";
                    description += $"{Config.BotPrefix}{cmdName}{aliasesStr}\n";
                }

                description += "```";
                eb.AddField(module.Name, description);
            }

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
        [Summary("Get DMed a list of commands or get help for an individual command.")]
        [Usage("{p}h {p}phrase", "{p}h")]
        [RequirePermissionsPass]
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
                    .WithDescription(command.Summary);

                if (!string.IsNullOrEmpty(command.Remarks)) eb.WithFooter(command.Remarks.Replace("{p}", Config.BotPrefix));

                if (command.Preconditions.Any(x => x is RequireBotPermissionAttribute))
                {
                    var botPerms = command.Preconditions.Where(x => x is RequireBotPermissionAttribute).ToList();
                    // do stuff with this, maybe add a field
                }

                if (command.Preconditions.FirstOrDefault(x => x is RequireUserPermissionAttribute) is
                    RequireUserPermissionAttribute userPerms && !(userPerms.GuildPermission is null))
                {
                    eb.Description += $"\nRequires **{string.Join("**, **", userPerms.GuildPermission.Value.GetFlags())}** permissions.";
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

            var guildConfig = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

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

            var channel = Context.Channel;
            if (Context.Guild.TryGetChannelId(guildConfig.SuggestionChannel.ToString(), out var channelId)
                && !((Context.Guild.GetChannel(channelId) as ISocketMessageChannel) is null))
                channel = Context.Guild.GetChannel(channelId) as ISocketMessageChannel;

            var msg = await channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
            await _suggestions.AddNewAsync(msg, Context.User as SocketGuildUser).ConfigureAwait(false);
            _logging.AddIgnoredMessages(new List<IMessage> {msg});
            await Context.Message.DeleteAsync().ConfigureAwait(false);

            var dm = await Context.Message.Author.GetOrCreateDMChannelAsync().ConfigureAwait(false);
            if (!(dm is null))
            {
                var eb2 = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Thanks for posting a suggestion!")
                    .WithDescription(
                        "Made it by mistake or screwed up your wording or spelling? Delete it by reacting with :wastebasket: (\\:wastebasket\\:)!");

                await dm.SendMessageAsync(string.Empty, embed: eb2.Build());
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
        [RequireContext(ContextType.Guild)]
        [RequirePermissionsPass]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [Priority(1)]
        private async Task ModifySuggestionAsync(long id)
        {
            var guild = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);
            var suggestionList = await _db.GetAsync<Suggestion>(x => x.Id == id).ConfigureAwait(false);

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
                                .WithThumbnailUrl(Context.Guild.GetUser((ulong) s.UserId).AvatarUrl())
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
                                .WithThumbnailUrl(Context.Guild.GetUser((ulong) s.UserId).AvatarUrl())
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