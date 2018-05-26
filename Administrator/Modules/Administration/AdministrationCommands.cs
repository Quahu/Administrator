using Administrator.Common;
using Administrator.Extensions;
using Administrator.Extensions.Attributes;
using Administrator.Services;
using Administrator.Services.Database;
using Administrator.Services.Database.Models;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Administrator.Modules.Administration.Services;

namespace Administrator.Modules.Administration
{
    public enum IgnoreMode
    {
        Bot,
        Pin,
        Me
    }

    [Name("Administration")]
    [RequireContext(ContextType.Guild)]
    public class AdministrationCommands : ModuleBase<SocketCommandContext>
    {
        private static readonly Config Config = BotConfig.New();
        private readonly DbService _db;
        private readonly LoggingService _logging;
        private readonly ChannelLockService _lock;

        public AdministrationCommands(DbService db, LoggingService logging, ChannelLockService @lock)
        {
            _db = db;
            _logging = logging;
            _lock = @lock;
        }

        #region Prune

        [Command("prune")]
        [Alias("delet", "clear")]
        [Summary("Delete messages in the current channel.")]
        [Remarks("Supply a user after the amount to prune only for a user, or \"ignore [bot/pin/me]\"")]
        [Usage("{p}prune 50", "{p}prune 25 ignore pin", "{p}prune 10 @SomeSpammer")]
        [RequireBotPermission(GuildPermission.ManageMessages | GuildPermission.ReadMessages)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [Priority(1)]
        private async Task PruneAsync(int count, [Remainder] IUser user = null)
        {
            if (!(Context.Channel is ITextChannel chnl)) return;

            if (user is null)
            {
                var messages = await chnl.GetMessagesAsync(Context.Message, Direction.Before, count + 1).FlattenAsync().ConfigureAwait(false);
                var allMessages = messages.Where(x => DateTimeOffset.UtcNow - x.Timestamp <= TimeSpan.FromDays(14))
                    .ToList();
                _logging.AddIgnoredMessages(allMessages);
                await chnl.DeleteMessagesAsync(allMessages).ConfigureAwait(false);
                await Context.Message.DeleteAsync().ConfigureAwait(false);
                return;
            }

            var m = await Context.Channel.GetMessagesAsync(Context.Message, Direction.Before, 1000).FlattenAsync()
                .ConfigureAwait(false);
            var toDelete = m.Where(x =>
                    x.Author.Id == user.Id && DateTimeOffset.UtcNow - x.Timestamp <= TimeSpan.FromDays(14)).Take(count)
                .ToList();
            _logging.AddIgnoredMessages(toDelete);
            await chnl.DeleteMessagesAsync(toDelete)
                .ConfigureAwait(false);
            await Context.Message.DeleteAsync().ConfigureAwait(false);
        }

        [Command("prune")]
        [Alias("delet", "clear")]
        [Summary("Delete messages in the current channel.")]
        [Remarks("Supply a user after the amount to prune only for a user, or \"ignore [bot/pin/me]\"")]
        [Usage("{p}prune 50", "{p}prune 25 ignore pin", "{p}prune 10 @SomeSpammer")]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [Priority(0)]
        private async Task PruneAsync(int count, string isIgnore, string modeStr)
        {
            if (!isIgnore.Equals("ignore", StringComparison.InvariantCultureIgnoreCase)
                || !(Context.Channel is ITextChannel chnl)) return;

            modeStr = char.ToUpper(modeStr[0]) + modeStr.Substring(1).ToLower();
            if (!Enum.TryParse(modeStr, out IgnoreMode mode)) return;

            var msgs = await chnl.GetMessagesAsync(Context.Message, Direction.Before, count + 1).FlattenAsync()
                .ConfigureAwait(false);
            var messages = msgs.Where(x => DateTimeOffset.UtcNow - x.Timestamp <= TimeSpan.FromDays(14)).ToList();

            switch (mode)
            {
                case IgnoreMode.Bot:
                    messages = messages.Where(x => !x.Author.IsBot).ToList();
                    break;
                case IgnoreMode.Pin:
                    messages = messages.Where(x => !x.IsPinned).ToList();
                    break;
                case IgnoreMode.Me:
                    messages = messages.Where(x => x.Author.Id != Context.User.Id).ToList();
                    break;
            }

            if (messages.Any())
            {
                _logging.AddIgnoredMessages(messages);
                await chnl.DeleteMessagesAsync(messages).ConfigureAwait(false);
                await Context.Message.DeleteAsync().ConfigureAwait(false);
            }
        }

        #endregion

        #region Warnings

        [Command("warn")]
        [Summary("Warn a user.")]
        [Usage("{p}warn @SomeRuleBreaker Please do not break our rules.")]
        [Remarks("If a user has accumulated enough warnings, they may be punished per your {p}warnpl list.")]
        [RequireBotPermission(GuildPermission.BanMembers)]
        [RequireUserPermission(GuildPermission.BanMembers)]
        private async Task WarnUserAsync(SocketGuildUser receiver, [Remainder] string reason = "-")
        {
            if (!(Context.User is SocketGuildUser issuer)) return;

            var inChannel = new EmbedBuilder();

            if (receiver.Id == Context.Message.Author.Id)
            {
                inChannel.WithErrorColor()
                    .WithDescription("You can't warn yourself, idiot.");
                await Context.Channel.EmbedAsync(inChannel.Build()).ConfigureAwait(false);
                return;
            }

            if (issuer.Hierarchy <= receiver.Hierarchy)
            {
                inChannel.WithErrorColor()
                    .WithDescription("You can't warn someone of the same rank as you or higher, idiot.");
                await Context.Channel.EmbedAsync(inChannel.Build()).ConfigureAwait(false);
                return;
            }

            var inDm = new EmbedBuilder()
                .WithWarnColor()
                .WithTitle($"You have been warned by user {Context.Message.Author}")
                .WithDescription(reason);

            var warning = new Warning
            {
                IssuerId = (long) Context.User.Id,
                ReceiverId = (long) receiver.Id,
                GuildId = (long) Context.Guild.Id,
                Reason = reason
            };

            await _db.InsertAsync(warning).ConfigureAwait(false);

            /*
            if (!receiver.Equals(Context.Message.Author) && !(receiver as SocketGuildUser).GuildPermissions.Has(GuildPermission.BanMembers))
            {
                await db.AddWarningAsync(receiver, Context.Message.Author, reason).ConfigureAwait(false);
            }
            else
            {
                inChannel.WithErrorColor()
                    .WithDescription("You cannot warn that user.");
                await Context.Channel.EmbedAsync(inChannel.Build()).ConfigureAwait(false);
                return;
            }
            */

            var warnings = await _db.GetAsync<Warning>(x => x.ReceiverId == (long) receiver.Id).ConfigureAwait(false);
            var warningPunishments = await _db.GetAsync<WarningPunishment>(x => x.GuildId == (long) Context.Guild.Id).ConfigureAwait(false);

            if (warningPunishments.FirstOrDefault(wp => warnings.Count == wp.Count) is WarningPunishment punishment)
            {
                switch (punishment.Punishment)
                {
                    case Punishment.Ban:
                        inChannel.WithWarnColor()
                            .WithDescription(
                                $"**{receiver}** has been warned and punishment **Ban** has been applied.");
                        await Context.Guild.AddBanAsync(receiver, 7, $"Warnings equalled {punishment.Count}")
                            .ConfigureAwait(false);
                        break;
                    case Punishment.Softban:
                        inChannel.WithWarnColor()
                            .WithDescription(
                                $"**{receiver}** has been warned and punishment **Ban** has been applied.");
                        await Context.Guild.AddBanAsync(receiver, 7, $"Warnings equalled {punishment.Count}")
                            .ConfigureAwait(false);
                        break;
                    case Punishment.Kick:
                        inChannel.WithWarnColor()
                            .WithDescription(
                                $"**{receiver}** has been warned and punishment **Kick** has been applied.");
                        await (receiver as SocketGuildUser).KickAsync($"Warnings equalled {punishment.Count}")
                            .ConfigureAwait(false);
                        break;
                    case Punishment.Mute:
                        inChannel.WithWarnColor()
                            .WithDescription(
                                $"**{receiver}** has been warned and punishment **Mute (30m)** has been applied.");
                        await InternalMuteAsync(receiver, DateTimeOffset.UtcNow.AddMinutes(30),
                            $"Reached {punishment.Count} warnings.").ConfigureAwait(false);
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(inChannel.Description))
                inChannel.WithWarnColor()
                    .WithDescription($"**{receiver}** has been warned.");

            await Context.Channel.EmbedAsync(inChannel.Build()).ConfigureAwait(false);
            try
            {
                var dmChannel = await receiver.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                await dmChannel.SendMessageAsync(string.Empty, embed: inDm.Build()).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        }

        [Command("warnings")]
        [Summary("View a user's warnings. Supply no user to see warnings you've given.")]
        [Remarks("If you've given more than ten warnings, supply a page number to view more.")]
        [Usage("{p}warnings @SomeIdiot", "{p}warnings")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [Priority(1)]
        private async Task GetWarningsAsync([Remainder]IUser receiver)
        {
            var eb = new EmbedBuilder()
                .WithWarnColor();

            var userWarnings = await _db.GetAsync<Warning>(x => x.ReceiverId == (long) receiver.Id && x.GuildId == (long) Context.Guild.Id).ConfigureAwait(false);

            eb.WithTitle($"Warnings for user {receiver}")
                .WithDescription("▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬");
            foreach (var w in userWarnings.OrderByDescending(x => x.TimeGiven).ToList())
                eb.AddField($"Id: {w.Id} | {w.TimeGiven:g} GMT | Given by {Context.Guild.GetUser((ulong)w.IssuerId)}",
                    $"{w.Reason.SanitizeMentions()}\n▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬");
            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        [Command("warnings")]
        [Summary("View a user's warnings. Supply no user to see warnings you've given.")]
        [Remarks("If you've given more than ten warnings, supply a page number to view more.")]
        [Usage("{p}warnings @SomeIdiot", "{p}warnings")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [Priority(0)]
        private async Task GetWarningsAsync(int page = 1)
        {
            var eb = new EmbedBuilder()
                .WithWarnColor();
            var userWarnings = await _db.GetAsync<Warning>(x => x.IssuerId == (long) Context.User.Id && x.GuildId == (long) Context.Guild.Id).ConfigureAwait(false);

            userWarnings = userWarnings.OrderByDescending(x => x.TimeGiven)
                .Skip(page - 1)
                .Take(10)
                .ToList();
            eb.WithTitle($"Warnings from user {Context.Message.Author}")
                .WithDescription("▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬");
            foreach (var w in userWarnings)
                eb.AddField($"Id: {w.Id} | {w.TimeGiven:g} GMT | Given to {Context.Guild.GetUser((ulong)w.ReceiverId)}",
                    $"{w.Reason}\n▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬");
            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }


        [Command("warnclear")]
        [Summary("Clear all warnings for a specific user, or a single warning given its ID.")]
        [Usage("{p}warnclear @SomeIdiot", "{p}warnclear 3")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [Priority(1)]
        private async Task ClearWarningAsync(IUser user)
        {
            var userWarnings = await _db.GetAsync<Warning>(x => x.ReceiverId == (long) user.Id).ConfigureAwait(false);
            if (userWarnings.Any())
            {
                var eb = new EmbedBuilder()
                    .WithWarnColor()
                    .WithDescription($"All warnings cleared for user **{user}**.");
                foreach (var warning in userWarnings) await _db.DeleteAsync(warning).ConfigureAwait(false);

                await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
            }
        }

        [Command("warnclear")]
        [Summary("Clear all warnings for a specific user, or a single warning given its ID.")]
        [Usage("{p}warnclear @SomeIdiot", "{p}warnclear 3")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [Priority(0)]
        private async Task ClearWarningAsync(long id)
        {
            var allWarnings = await _db.GetAsync<Warning>(x => x.Id == id).ConfigureAwait(false);

            if (allWarnings.FirstOrDefault() is Warning w)
            {
                var eb = new EmbedBuilder()
                    .WithWarnColor()
                    .WithTitle($"Removed warning with ID {w.Id}:")
                    .AddField($"Id: {w.Id} | {w.TimeGiven:g} | Given by {Context.Guild.GetUser((ulong)w.IssuerId)}",
                        $"**Reason:** {w.Reason}\n▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬");
                await _db.DeleteAsync(w).ConfigureAwait(false);
                await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
            }
        }

        [Command("warnpunish")]
        [Alias("warnp")]
        [Summary(
            "Set or update a warning punishment. The first argument is the number of warnings to receive the punishment, the second argument is type of punishment. Supply no type to remove the punishment for those number of warnings.")]
        [Usage("{p}warnp 3 Kick", "{p}warnp 5")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [Priority(1)]
        private async Task ModifyPunishmentAsync(long count)
        {
            var warningPunishments = await _db.GetAsync<WarningPunishment>(x => x.GuildId == (long) Context.Guild.Id);
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (warningPunishments.FirstOrDefault(x => x.Count == count && x.GuildId == (long) Context.Guild.Id) is WarningPunishment wp)
            {
                await _db.DeleteAsync(wp).ConfigureAwait(false);
                var eb = new EmbedBuilder()
                    .WithWarnColor()
                    .WithDescription($"I will no longer apply a punishment to users who reach **{wp.Count}** warning(s).");
                await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
                gc.HasModifiedWarningPunishments = true;
                await _db.UpdateAsync(gc).ConfigureAwait(false);
            }
        }

        [Command("warnpunish")]
        [Alias("warnp")]
        [Summary(
            "Set or update a warning punishment. The first argument is the number of warnings to receive the punishment, the second argument is type of punishment. Supply no type to remove the punishment for those number of warnings.")]
        [Usage("{p}warnp 3 Kick", "{p}warnp 5")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [Priority(0)]
        private async Task ModifyPunishmentAsync(long count, [Remainder] string type)
        {
            if (count < 1) return;
            type = type[0].ToString().ToUpper() + type.Substring(1).ToLower();
            var eb = new EmbedBuilder();
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);
            if (Enum.TryParse(type, out Punishment punishment))
            {
                var wps = await _db
                    .GetAsync<WarningPunishment>(x => x.GuildId == (long) Context.Guild.Id && x.Count == count)
                    .ConfigureAwait(false);

                if (wps.FirstOrDefault() is WarningPunishment wp)
                {
                    wp.PunishmentId = (long) punishment;
                    await _db.UpdateAsync(wp).ConfigureAwait(false);
                }
                else
                {
                    var newWp = new WarningPunishment
                    {
                        Count = count,
                        GuildId = (long) Context.Guild.Id,
                        PunishmentId = (long) punishment
                    };
                    await _db.InsertAsync(newWp).ConfigureAwait(false);
                }

                eb.WithOkColor()
                    .WithDescription(
                        $"I will now apply punishment **{punishment}** to users who reach {count} warnings.");

                gc.HasModifiedWarningPunishments = true;
                await _db.UpdateAsync(gc).ConfigureAwait(false);
            }
            else
            {
                eb.WithErrorColor()
                    .WithDescription(
                        "Could not parse the punishment type from input. Valid types are: Mute, Kick, Softban, Ban");
            }
            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        [Command("warnpunishlist")]
        [Alias("warnpl")]
        [Summary("View the current warning punishments.")]
        [Usage("{p}warnpl")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        private async Task ListWarningPunishmentsAsync()
        {
            var all = await _db.GetAsync<WarningPunishment>(x => x.GuildId == (long) Context.Guild.Id).ConfigureAwait(false);
            var eb = new EmbedBuilder()
                .WithWarnColor();
            if (all.Any())
            {
                eb.WithTitle($"Warning punishments for guild {Context.Guild.Name}")
                    .WithDescription(string.Join("\n",
                        all.OrderBy(x => x.Count).Select(x => $"**{x.Count}** => {x.Punishment}")));
            }
            else
            {
                eb.WithDescription("No warning punishments set up for this guild.");
            }

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        #endregion

        #region Notes

        [Command("note")]
        [Summary("Add a note for a user.")]
        [Usage("{p}note @SomeEdgyDude Threatened to raid the server.")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [RequireContext(ContextType.Guild)]
        private async Task AddNoteAsync(IUser user, [Remainder] string note)
        {
            var eb = new EmbedBuilder();

            var modNote = new ModNote
            {
                IssuerId = (long) Context.User.Id,
                ReceiverId = (long) user.Id,
                Note = note
            };

            if (await _db.InsertAsync(modNote) > 0)
                eb.WithWarnColor()
                    .WithCurrentTimestamp()
                    .WithTitle($"Note added for user {user}")
                    .WithDescription(note.SanitizeMentions())
                    .WithFooter($"Added by {Context.Message.Author}");
            else
                eb.WithErrorColor()
                    .WithDescription("Could not add a note for this user. Please report this.");

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        [Command("noteremove")]
        [Alias("noterm")]
        [Summary("Remove a single note given its ID.")]
        [Usage("{p}noterm 6")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [RequireContext(ContextType.Guild)]
        private async Task RemoveNoteAsync(long id)
        {
            var eb = new EmbedBuilder();
            var notes = await _db.GetAsync<ModNote>(x => x.Id == id).ConfigureAwait(false);

            if (notes.FirstOrDefault() is ModNote mn)
            {
                await _db.DeleteAsync(mn).ConfigureAwait(false);
                eb.WithWarnColor()
                    .WithDescription($"Mod note with ID **{id}** has been removed.");
            }
            else
                eb.WithErrorColor()
                    .WithDescription("No mod note found with that ID.");

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        [Command("notes")]
        [Summary("View a user's notes. Supply no user to see notes you've given.")]
        [Usage("{p}notes @SomeUser", "{p}notes")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        private async Task GetNotesAsync(IUser receiver)
        {
            var eb = new EmbedBuilder();
            var notes = await _db.GetAsync<ModNote>(x => x.ReceiverId == (long) receiver.Id).ConfigureAwait(false);

            if (notes.Any())
            {
                eb.WithWarnColor()
                    .WithTitle($"Mod notes for user {receiver}")
                    .WithDescription("▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬");

                foreach (var note in notes.OrderByDescending(n => n.TimeGiven))
                    eb.AddField($"Id: {note.Id} | {note.TimeGiven:g} GMT | Given by {Context.Guild.GetUser((ulong) note.IssuerId)}",
                        $"{note.Note.SanitizeMentions()}\n▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬");
            }
            else
            {
                eb.WithErrorColor()
                    .WithDescription("No mod notes found for that user.");
            }

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        [Command("notes")]
        [Summary("View a user's notes. Supply no user to see notes you've given.")]
        [Usage("{p}notes @SomeUser", "{p}notes")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [RequireContext(ContextType.Guild)]
        [Priority(0)]
        private async Task GetNotesAsync(int page = 1)
        {
            var eb = new EmbedBuilder();
            var notes = await _db.GetAsync<ModNote>(x => x.IssuerId == (long) Context.User.Id).ConfigureAwait(false);

            if (notes.Any())
            {
                eb.WithWarnColor()
                    .WithTitle($"Mod notes from user {Context.Message.Author}")
                    .WithDescription("▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬")
                    .WithFooter($"{page} / {notes.Count / 10 + 1}");

                foreach (var note in notes.OrderByDescending(n => n.TimeGiven).Skip((page - 1) * 10).Take(10))
                    eb.AddField($"Id: {note.Id} | {note.TimeGiven:g} GMT | Note for {Context.Guild.GetUser((ulong)note.ReceiverId)}",
                        $"{note.Note}\n▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬");
            }
            else
            {
                eb.WithErrorColor()
                    .WithDescription("No mod notes found from that user.");
            }

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        #endregion

        #region Mute

        [Command("mute")]
        [Summary("Mutes a user for an optional amount of time and reason.")]
        [Usage("{p}mute @SomeSpammer", "{p}mute @SomeSpammer 1d", "{p}mute @SomeSpammer 1d shut up nerd")]
        [Remarks("Time must be of format w/d/h/m, eg. 1w2d5h10m. All are optional, eg. 10m, 1d, 1d6h")]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        [RequireUserPermission(GuildPermission.MuteMembers)]
        [Priority(2)]
        private async Task MuteUserAsync(IUser targetUser)
            => await InternalMuteAsync(targetUser, DateTimeOffset.MaxValue, "No reason specified.").ConfigureAwait(false);

        [Command("mute")]
        [Summary("Mutes a user for an optional amount of time and reason.")]
        [Usage("{p}mute @SomeSpammer", "{p}mute @SomeSpammer 1d", "{p}mute @SomeSpammer 1d shut up nerd")]
        [Remarks("Time must be of format w/d/h/m, eg. 1w2d5h10m. All are optional, eg. 10m, 1d, 1d6h")]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        [RequireUserPermission(GuildPermission.MuteMembers)]
        [Priority(1)]
        private async Task MuteUserAsync(IUser target, string input)
        {
            if (input.TryParseTimeSpan(out var duration))
            {
                await InternalMuteAsync(target, DateTimeOffset.UtcNow.Add(duration), "No reason specified.").ConfigureAwait(false);
            }
            else
            {
                await InternalMuteAsync(target, DateTimeOffset.MaxValue, input).ConfigureAwait(false);
            }
        }

        [Command("mute")]
        [Summary("Mutes a user.")]
        [Usage("{p}mute @SomeSpammer")]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        [RequireUserPermission(GuildPermission.MuteMembers)]
        [Priority(0)]
        private async Task MuteUserAsync(IUser target, string durationStr, [Remainder]string reason)
        {
            if (durationStr.TryParseTimeSpan(out var duration))
            {
                await InternalMuteAsync(target, DateTimeOffset.UtcNow.Add(duration), reason).ConfigureAwait(false);
            }
            else
            {
                await Context.Message.AddReactionAsync(new Emoji("\U0000274c")).ConfigureAwait(false);
            }
        }

        private async Task InternalMuteAsync(IUser targetUser, DateTimeOffset ending, string reason)
        {
            var eb = new EmbedBuilder();
            var guildConfig = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (!(Context.User is SocketGuildUser issuer) || !(targetUser is SocketGuildUser target)) return;

            if (!(Context.Guild.Roles.FirstOrDefault(x => x.Id == (ulong) guildConfig.MuteRole) is SocketRole muteRole))
            {
                await Context.Channel.SendErrorAsync($"Guild does not have a mute role set up. Check {Config.BotPrefix}mgc to see if the mute role was set.")
                    .ConfigureAwait(false);
                return;
            }

            if (issuer.Hierarchy > target.Hierarchy)
            {
                var mute = new MutedUser
                {
                    UserId = (long) targetUser.Id,
                    Ending = ending,
                    GuildId = (long) Context.Guild.Id,
                    Reason = reason
                };

                if (await _db.InsertAsync(mute).ConfigureAwait(false) > 0)
                {
                    await target.AddRoleAsync(muteRole).ConfigureAwait(false);
                    eb.WithOkColor()
                        .WithDescription($"User **{target}** has been muted until {ending:g} GMT.");
                }
                else
                {
                    eb.WithErrorColor()
                        .WithDescription(
                            "Either that user already has a mute in place, or an internal error occurred.");

                    await _db.DeleteAsync(mute).ConfigureAwait(false);
                }
            }
            else
            {
                eb.WithErrorColor()
                    .WithDescription("You don't have permission to do that.");
            }

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        [Command("unmute")]
        [Summary("Unmutes a user by name or mute ID.")]
        [Usage("{p}unmute @SomeGoodDude")]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        [RequireUserPermission(GuildPermission.MuteMembers)]
        [Priority(1)]
        private async Task UnmuteAsync(long id)
        {
            var guildConfig = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (!(Context.Guild.Roles.FirstOrDefault(x => x.Id == (ulong) guildConfig.MuteRole) is SocketRole muteRole))
            {
                await Context.Channel.SendErrorAsync($"Guild does not have a mute role set up. Check {Config.BotPrefix}mgc to see if the mute role was set.")
                    .ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder();
            var mutes = await _db.GetAsync<MutedUser>(x => x.GuildId == (long) Context.Guild.Id && x.Id == id).ConfigureAwait(false);

            if (mutes.FirstOrDefault() is MutedUser mute
                && Context.Guild.GetUser((ulong) mute.UserId) is SocketGuildUser target)
            {
                await target.RemoveRoleAsync(muteRole).ConfigureAwait(false);
                eb.WithOkColor()
                    .WithDescription($"User **{target}** has been unmuted.");
                await _db.DeleteAsync(mute).ConfigureAwait(false);
            }
            else
            {
                eb.WithErrorColor()
                    .WithDescription("Couldn't unmute that user. Are they already unmuted?");
            }

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        [Command("unmute")]
        [Summary("Unmutes a user by name or mute ID.")]
        [Usage("{p}unmute @SomeGoodDude")]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        [RequireUserPermission(GuildPermission.MuteMembers)]
        [Priority(0)]
        private async Task UnmuteAsync(IUser targetUser)
        {
            if (!(targetUser is SocketGuildUser target)) return;
            var guildConfig = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (!(Context.Guild.Roles.FirstOrDefault(x => x.Id == (ulong) guildConfig.MuteRole) is SocketRole muteRole))
            {
                await Context.Channel.SendErrorAsync($"Guild does not have a mute role set up. Check {Config.BotPrefix}mgc to see if the mute role was set.")
                    .ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder();
            var mutes = await _db.GetAsync<MutedUser>(x => x.GuildId == (long) Context.Guild.Id && x.UserId == (long) targetUser.Id).ConfigureAwait(false);

            if (mutes.FirstOrDefault() is MutedUser mute)
            {
                await target.RemoveRoleAsync(muteRole).ConfigureAwait(false);
                eb.WithOkColor()
                    .WithDescription($"User **{target}** has been unmuted.");
                await _db.DeleteAsync(mute).ConfigureAwait(false);
            }
            else
            {
                eb.WithErrorColor()
                    .WithDescription("Couldn't unmute that user. Is there a mute by that ID?");
            }

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }


        [Command("mutes")]
        [Summary("View guild user mutes. Supply a page number to view more.")]
        [Usage("{p}mutes")]
        [RequireUserPermission(GuildPermission.MuteMembers)]
        private async Task GetMutesAsync(int page = 1)
        {
            var mutes = await _db.GetAsync<MutedUser>(x => x.GuildId == (long) Context.Guild.Id).ConfigureAwait(false);

            if (!mutes.Any())
            {
                await Context.Channel.SendErrorAsync("No mutes found for this guild.").ConfigureAwait(false);
                return;
            }

            if ((page - 1) * 20 - mutes.Count >= 20)
            {
                await Context.Channel.SendErrorAsync("No mutes found on that page.").ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle($"Mutes for {Context.Guild.Name}")
                .WithDescription(string.Join("\n\n",
                    mutes.Skip((page - 1) * 20).Take(20).Select(x =>
                        $"**{x.Id}** - {Context.Guild.GetUser((ulong) x.UserId)?.ToString()} - expires {x.Ending:g}")));

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }
        
        #endregion

        #region General

        [Command("makepingable", RunMode = RunMode.Async)]
        [Alias("pingable", "mentionable")]
        [Summary("Makes a role mentionable for as many seconds as you specify. Defaults to 30. Minimum 5.")]
        [Usage("{p}pingable 60 Some Role", "{p}pingable Some Role")]
        [RequireUserPermission(GuildPermission.ManageRoles)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        [Priority(1)]
        private async Task MakeRolePingableAsync(uint seconds, [Remainder] string roleName)
        {
            if (seconds < 5) return;
            if (!(Context.Guild.Roles.FirstOrDefault(x => x.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase))
                is SocketRole role))
            {
                await Context.Channel.SendErrorAsync("No role found by that name.").ConfigureAwait(false);
                return;
            }

            try
            {
                await role.ModifyAsync(x => x.Mentionable = true).ConfigureAwait(false);
                var m = await Context.Channel
                    .SendConfirmAsync($"Role **{role.Name}** will now be mentionable for the next {seconds} seconds.")
                    .ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(seconds)).ConfigureAwait(false);
                await role.ModifyAsync(x => x.Mentionable = false).ConfigureAwait(false);
                var modified = new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription($"Role **{role.Name}** has been made unmentionable.");
                await m.ModifyAsync(x => x.Embed = modified.Build()).ConfigureAwait(false);
            }
            catch
            {
                await Context.Channel
                    .SendErrorAsync(
                        "I could not make that role mentionable. Check my permissions and verify that I am above this role in the hierarchy.")
                    .ConfigureAwait(false);
            }
        }

        [Command("makepingable", RunMode = RunMode.Async)]
        [Alias("pingable", "mentionable")]
        [Summary("Makes a role mentionable for as many seconds as you specify. Defaults to 30. Minimum 5.")]
        [Usage("{p}pingable 60 Some Role", "{p}pingable Some Role")]
        [RequireUserPermission(GuildPermission.ManageRoles)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        [Priority(0)]
        private async Task MakeRolePingableAsync([Remainder] string roleName)
            => await MakeRolePingableAsync(30, roleName).ConfigureAwait(false);
        
        [Command("lockchannel", RunMode = RunMode.Async)]
        [Alias("lock")]
        [Summary(
            "Lock a channel, sealing it off for everyone below your guild's PermRole. A duration may be specified, in seconds.")]
        [Usage("{p}lock #somechannel", "{p}lock #somechannel 60")]
        [Remarks("Note: on a permanent lock, this command removes all existing permission overwrites for that channel.")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        [RequirePermRole]
        [Priority(1)]
        private async Task LockChannelAsync(IGuildChannel channel, uint seconds)
        {
            if (!(channel is ITextChannel)) return;
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);
            if (!(Context.Guild.Roles.FirstOrDefault(x => x.Id == (ulong) gc.PermRole) is SocketRole permRole))
            {
                // this shouldn't happen, but let's check anyways
                await Context.Channel.SendErrorAsync("This guild's permrole is not set.").ConfigureAwait(false);
                return;
            }
        }

        [Command("lockchannel", RunMode = RunMode.Async)]
        [Alias("lock")]
        [Summary(
            "Lock a channel, sealing it off for everyone below your guild's PermRole. A duration may be specified, in seconds.")]
        [Usage("{p}lock #somechannel", "{p}lock #somechannel 60")]
        [Remarks("Note: on a permanent lock, this command removes all existing permission overwrites for that channel.")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        [RequirePermRole]
        [Priority(0)]
        private async Task LockChannelAsync(IGuildChannel channel)
        {
            if (!(channel is ITextChannel chnl)) return;
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);
            if (!(Context.Guild.Roles.FirstOrDefault(x => x.Id == (ulong) gc.PermRole) is SocketRole permRole))
            {
                // this shouldn't happen, but let's check anyways
                await Context.Channel.SendErrorAsync("This guild's permrole is not set.").ConfigureAwait(false);
                return;
            }
            
            var disable = new OverwritePermissions(sendMessages: PermValue.Deny, addReactions: PermValue.Deny);
            var enable = new OverwritePermissions(sendMessages: PermValue.Allow, addReactions: PermValue.Allow);

            try
            {
                // remove existing perms
                foreach (var o in chnl.PermissionOverwrites.ToList())
                {
                    switch (o.TargetType)
                    {
                        case PermissionTarget.Role:
                            if (Context.Guild.Roles.FirstOrDefault(x => x.Id == o.TargetId) is SocketRole r)
                                await chnl.RemovePermissionOverwriteAsync(r).ConfigureAwait(false);
                            break;
                        case PermissionTarget.User:
                            if (Context.Guild.GetUser(o.TargetId) is SocketGuildUser u)
                                await chnl.RemovePermissionOverwriteAsync(u).ConfigureAwait(false);
                            break;
                    }
                }

                await chnl.AddPermissionOverwriteAsync(Context.Guild.EveryoneRole, disable).ConfigureAwait(false);
                await chnl.AddPermissionOverwriteAsync(permRole, enable).ConfigureAwait(false);
            }
            catch
            {
                await Context.Channel.SendErrorAsync("An error occurred setting up lock permissions.")
                    .ConfigureAwait(false);
                return;
            }

            await chnl.SendConfirmAsync($"{chnl.Mention} is now locked.").ConfigureAwait(false);
        }

        [Command("say")]
        [Summary("Send a message in a specified channel using the bot. Supports TOML embeds. Channel defaults to the current channel you are in.")]
        [Usage("{p}say #somechannel Please move the conversation to #someotherchannel.")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [Priority(1)]
        private async Task SayAsync(IGuildChannel channel, [Remainder] string message)
        {
            if (Context.Guild.TextChannels.FirstOrDefault(x => x.Id == channel.Id) is ISocketMessageChannel chnl)
            {
                try
                {
                    var a = TomlEmbedBuilder.ReadToml(message);
                    if (a is TomlEmbed e)
                    {
                        await chnl.EmbedAsync(e).ConfigureAwait(false);
                    }
                }
                catch
                {
                    await chnl.SendMessageAsync(message).ConfigureAwait(false);
                }
            }
        }

        [Command("say")]
        [Summary(
            "Send a message in a specified channel using the bot. Supports TOML embeds. Channel defaults to the current channel you are in.")]
        [Usage("{p}say #somechannel Please move the conversation to #someotherchannel.")]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [Priority(0)]
        private async Task SayAsync([Remainder] string message)
        {
            await SayAsync(Context.Channel as IGuildChannel, message).ConfigureAwait(false);

            try
            {
                await Context.Message.DeleteAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        }

        [Command("edit")]
        [Summary(
            "Edit a bot message with a specific channel and message ID. Supports TOML embeds. Channel defaults to the current channel you are in.")]
        [Usage("{p}edit #somechannel 1234567890 haha memes")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [Priority(1)]
        private async Task EditBotMessageAsync(IGuildChannel channel, ulong messageId, [Remainder] string newMessage)
        {
            if (!(channel is ISocketMessageChannel c && channel.Guild.Id == Context.Guild.Id)) return;
            if (string.IsNullOrWhiteSpace(newMessage)) return;

            var msg = await c.GetMessageAsync(messageId).ConfigureAwait(false);

            if (msg is IUserMessage m)
            {
                try
                {
                    var a = TomlEmbedBuilder.ReadToml(newMessage);
                    if (a is TomlEmbed e)
                    {
                        var toModify = e.ToMessage();
                        await m.ModifyAsync(x =>
                        {
                            x.Content = toModify.Item1;
                            x.Embed = toModify.Item2?.Build();
                        }).ConfigureAwait(false);
                    }
                }
                catch
                {
                    await m.ModifyAsync(x => x.Content = newMessage).ConfigureAwait(false);
                }

                await Context.Message.AddReactionAsync(new Emoji("\U00002705")).ConfigureAwait(false);
                return;
            }

            await Context.Message.AddReactionAsync(new Emoji("\U0000274c")).ConfigureAwait(false);
        }

        [Command("edit")]
        [Summary(
            "Edit a bot message with a specific channel and message ID. Supports TOML embeds. Channel defaults to the current channel you are in.")]
        [Usage("{p}edit #somechannel 1234567890 haha memes")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [Priority(0)]
        private async Task EditBotMessageAsync(ulong messageId, [Remainder] string newMessage)
            => await EditBotMessageAsync(Context.Channel as IGuildChannel, messageId, newMessage).ConfigureAwait(false);

        [Command("kick")]
        [Alias("k")]
        [Summary("Kick a user with an optional reason.")]
        [Usage("{p}kick @SomeRuleBreaker")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        [RequireBotPermission(GuildPermission.KickMembers)]
        private async Task KickUserAsync(IUser user, [Remainder] string reason = "-")
        {
            if (!(user is SocketGuildUser target) || !(Context.User is SocketGuildUser issuer)) return;

            if (issuer.Hierarchy <= target.Hierarchy || issuer.Id == target.Id)
            {
                await Context.Channel.SendErrorAsync("You don't have permission to do that.").ConfigureAwait(false);
                return;
            }

            var inChannel = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle($"**{issuer}** kicked user {target}")
                .WithDescription(reason);

            var inDm = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle($"You have been kicked from {Context.Guild.Name} by {Context.User}")
                .WithDescription(reason);

            try
            {
                var dm = await user.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                await dm.EmbedAsync(inDm.Build()).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            await target.KickAsync(reason).ConfigureAwait(false);
            await Context.Channel.EmbedAsync(inChannel.Build()).ConfigureAwait(false);
        }

        [Command("softban")]
        [Alias("sb")]
        [Summary("Softban a user with an optional reason. A \"softban\" is essentially a ban + unban, pruning the user's messages as well.")]
        [Usage("{p}sb @SomeSpammer Do not spam.")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        private async Task SoftbanUserAsync(IUser user, [Remainder] string reason = "-")
        {
            if (!(user is SocketGuildUser target) || !(Context.User is SocketGuildUser issuer)) return;

            if (issuer.Hierarchy <= target.Hierarchy || issuer.Id == target.Id)
            {
                await Context.Channel.SendErrorAsync("You don't have permission to do that.").ConfigureAwait(false);
                return;
            }

            var inChannel = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle($"**{issuer}** softbanned user {target}")
                .WithDescription(reason);

            var inDm = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle($"You have been softbanned from {Context.Guild.Name} by {Context.User}")
                .WithDescription(reason);

            try
            {
                var dm = await user.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                await dm.EmbedAsync(inDm.Build()).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            await Context.Guild.AddBanAsync(target, 7, reason).ConfigureAwait(false);
            await Context.Channel.EmbedAsync(inChannel.Build()).ConfigureAwait(false);
            await Context.Guild.RemoveBanAsync(target).ConfigureAwait(false);
        }

        [Command("ban")]
        [Alias("b")]
        [Summary("Ban a user with an optional reason.")]
        [Usage("{p}ban @SomeIdiot Your kind are not welcome here.")]
        [RequireBotPermission(GuildPermission.BanMembers)]
        [RequireUserPermission(GuildPermission.BanMembers)]
        private async Task BanUserAsync(IGuildUser user, [Remainder] string reason = "-")
        {
            if (!(user is SocketGuildUser target) || !(Context.User is SocketGuildUser issuer)) return;

            if (issuer.Hierarchy <= target.Hierarchy || issuer.Id == target.Id)
            {
                await Context.Channel.SendErrorAsync("You don't have permission to do that.").ConfigureAwait(false);
                return;
            }

            var inChannel = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle($"**{issuer}** banned user {target}")
                .WithDescription(reason);

            var inDm = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle($"You have been banned from {Context.Guild.Name} by {Context.User}")
                .WithDescription(reason);

            try
            {
                var dm = await user.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                await dm.EmbedAsync(inDm.Build()).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            await Context.Guild.AddBanAsync(target, 7, reason).ConfigureAwait(false);
            await Context.Channel.EmbedAsync(inChannel.Build()).ConfigureAwait(false);
        }

        [Command("hackban")]
        [Summary("Ban a user, even if they aren't on the server, by their user ID.")]
        [Usage("{p}hackban 0123456789 Don't come back.")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        private async Task HackbanUserAsync(ulong userId, [Remainder] string reason = "-")
        {
            var eb = new EmbedBuilder();
            if (Context.Guild.Users.FirstOrDefault(x => x.Id == userId) is SocketGuildUser target
                && Context.User is SocketGuildUser user
                && user.Hierarchy < target.Hierarchy)
            {
                eb.WithErrorColor()
                    .WithDescription("You don't have permission to do that.");
            }
            else
            {
                await Context.Guild.AddBanAsync(userId, 7, reason).ConfigureAwait(false);
                var bans = await Context.Guild.GetBansAsync().ConfigureAwait(false);

                if (bans.FirstOrDefault(b => b.User.Id == userId) is RestBan ban)
                {
                    eb.WithOkColor()
                        .WithTitle($"**{Context.User}** hackbanned user {ban.User}")
                        .WithDescription(reason);
                }
                else
                {
                    eb.WithErrorColor()
                        .WithDescription("Could not find a user matching that ID, or could not ban them.");
                }
            }

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        [Command("userinfo")]
        [Alias("uinfo")]
        [Summary("Get info about a user. Defaults to yourself.")]
        [Usage("{p}uinfo", "{p}uinfo @SomeUser")]
        //[RequireUserPermission(GuildPermission.ManageMessages)]
        private async Task UserInfoAsync([Remainder] IGuildUser user = null)
        {
            if (!((user ?? Context.User as SocketGuildUser) is SocketGuildUser usr))
            {
                await Context.Channel.SendErrorAsync("User not found.").ConfigureAwait(false);
                return;
            }
            var eb = new EmbedBuilder();
            var color = usr.Roles.OrderByDescending(r => r.Position).FirstOrDefault(x => !x.IsEveryone)?.Color;
            if (color is null)
                eb.WithOkColor();
            else
                eb.WithColor(color.Value);
            eb.WithTitle($"User info for {usr}")
                .WithThumbnailUrl(usr.GetAvatarUrl())
                .AddField("Mention", usr.Mention)
                .AddField("Nickname", string.IsNullOrWhiteSpace(usr.Nickname) ? "N/A" : usr.Nickname, true)
                .AddField("Id", usr.Id, true)
                .AddField("Joined server", usr.JoinedAt is null ? "N/A" : $"{usr.JoinedAt:g}", true)
                .AddField("Joined Discord", $"{usr.CreatedAt:g} GMT", true)
                .AddField("Roles", usr.Roles.Any(x => !x.IsEveryone) ? string.Join("\n", usr.Roles.Where(x => !x.IsEveryone).Select(r => r.Name)) : "None");

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        #endregion
    }
}