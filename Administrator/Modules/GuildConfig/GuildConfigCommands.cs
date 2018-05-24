using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Administrator.Common;
using Administrator.Extensions;
using Administrator.Extensions.Attributes;
using Administrator.Services;
using Administrator.Services.Database;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Nett;

namespace Administrator.Modules.GuildConfig
{
    [Name("GuildConfig")]
    [RequireContext(ContextType.Guild)]
    public class GuildConfigCommands : ModuleBase<SocketCommandContext>
    {
        private static readonly Config Config = BotConfig.New();
        private readonly DbService _db;

        public GuildConfigCommands(DbService db)
        {
            _db = db;
        }

        [Command("permrole")]
        [Summary("Gets or sets this guild's permrole. The permrole is needed for many administrative commands.\nSetting the permrole requires **Administrator** permissions or guild ownership.")]
        [Usage("{p}permrole Admin")]
        private async Task GetOrSetPermRoleAsync([Remainder] string role = null)
        {
            if (!(Context.User is SocketGuildUser user)) return;
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(role))
            {
                if (Context.Guild.Roles.FirstOrDefault(x => x.Id == (ulong) gc.PermRole) is SocketRole r)
                {
                    await Context.Channel.SendConfirmAsync($"This guild's permrole is currently set to role **{r.Name}** (`{r.Id}`).")
                        .ConfigureAwait(false);
                    return;
                }

                await Context.Channel.SendErrorAsync("This guild's permrole is currently not set, or was deleted.")
                    .ConfigureAwait(false);
                return;
            }

            if (user.Roles.All(x => !x.Permissions.Administrator) || Context.Guild.OwnerId != user.Id)
            {
                await Context.Channel
                    .SendErrorAsync("You must have **Administrator** permissions (or be the guild owner) to set the permrole!")
                    .ConfigureAwait(false);
                return;
            }

            if (Context.Guild.Roles.FirstOrDefault(x =>
                x.Name.Equals(role, StringComparison.InvariantCultureIgnoreCase)
                || ulong.TryParse(role, out var result) && x.Id == result) is SocketRole ro)
            {
                gc.PermRole = (long) ro.Id;
                await _db.UpdateAsync(gc).ConfigureAwait(false);
                await Context.Channel
                    .SendConfirmAsync($"This guild's permrole has been set to role **{ro.Name}** (`{ro.Id}`).")
                    .ConfigureAwait(false);
                return;
            }

            await Context.Channel.SendErrorAsync("Could not find a role by that name or ID to set as the permrole.")
                .ConfigureAwait(false);
        }

        [Command("showguildconfig")]
        [Alias("showgc", "sgc")]
        [Summary("Displays all bot configuration settings for this guild.")]
        [Usage("{p}showguildconfig")]
        [Remarks("Note: to view the greet message, use {p}greetmsg.")]
        [RequirePermRole]
        private async Task ShowGuildConfigAsync()
        {
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle($"Bot configuration for {Context.Guild.Name}")
                .WithThumbnailUrl(Context.Guild.IconUrl)
                .WithDescription(
                    $"**LogChannel**: {(gc.LogChannel == default ? "Not set" : Context.Guild.GetTextChannel((ulong) gc.LogChannel)?.Mention)}\n" +
                    $"**SuggestionChannel**: {(gc.SuggestionChannel == default ? "Not set" : Context.Guild.GetTextChannel((ulong) gc.SuggestionChannel)?.Mention)}\n" +
                    $"**SuggestionArchive**: {(gc.SuggestionArchive == default ? "Not set" : Context.Guild.GetTextChannel((ulong) gc.SuggestionArchive)?.Mention)}\n" +
                    $"**GreetChannel**: {(gc.GreetChannel == default ? "Not set" : Context.Guild.GetTextChannel((ulong) gc.GreetChannel)?.Mention)}\n\n" +
                    $"**PermRole**: {(Context.Guild.GetRole((ulong) gc.PermRole) is SocketRole permRole ? permRole.Name : "Not set")}\n" +
                    $"**MuteRole**: {(Context.Guild.GetRole((ulong) gc.MuteRole) is SocketRole muteRole ? muteRole.Name : "Not set")}\n" +
                    $"**LookingToPlayRole**: {(Context.Guild.GetRole((ulong) gc.LookingToPlayRole) is SocketRole ltpRole ? ltpRole.Name : "Not set")}\n" +
                    $"**LookingToPlayMaxHours**: {(gc.LookingToPlayMaxHours == default ? "None" : $"{gc.LookingToPlayMaxHours} hours")}\n\n" +
                    $"**UpvoteArrow**: {gc.UpvoteArrow}\n" +
                    $"**DownvoteArrow**: {gc.DownvoteArrow}\n\n" +
                    $"**VerboseErrors**: {gc.VerboseErrors}\n" +
                    $"**GreetUserOnJoin**: {gc.GreetUserOnJoin}\n" +
                    $"**GreetTimeout**: {(gc.GreetTimeout == default ? "Disabled" : $"{gc.GreetTimeout} seconds")}\n" + 
                    $"**EnableRespects**: {gc.EnableRespects}\n" +
                    $"**InviteFiltering**: {gc.InviteFiltering}\n" +
                    $"**ServerInvite**: {(string.IsNullOrWhiteSpace(gc.InviteCode) ? "Not set" : $"https://discord.gg/{gc.InviteCode}")}\n" +
                    $"**PhraseMinLength**: {gc.PhraseMinLength} characters");

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        [Command("invitecode")]
        [Summary(
            "Gets or sets the guild's invite code. Using {p}invite will grab this code and create a link that users can share to invite others.")]
        [Usage("{p}invitecode 08bddQ")]
        [Remarks("Note: only the code is needed, not the full link.")]
        [RequirePermRole]
        private async Task GetOrSetInviteCodeAsync(string code = null)
        {
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(code) || gc.InviteCode == code)
            {
                if (string.IsNullOrWhiteSpace(gc.InviteCode))
                {
                    await Context.Channel.SendErrorAsync("This guild's invite link is currently not set.").ConfigureAwait(false);
                    return;
                }

                await Context.Channel
                    .SendConfirmAsync(
                        $"This guild's invite link is currently set to https://discord.gg/{gc.InviteCode} .")
                    .ConfigureAwait(false);

                return;
            }

            gc.InviteCode = code;
            await _db.UpdateAsync(gc).ConfigureAwait(false);

            await Context.Channel
                .SendConfirmAsync($"This guild's invite link has been set to https://discord.gg/{code} .")
                .ConfigureAwait(false);
        }

        [Command("muterole")]
        [Summary(
            "Gets or sets this guild's mute role. Users who are muted with {p}mute will have this role applied to them.")]
        [Usage("{p}muterole Silenced")]
        [Remarks(
            "Note: When you set the mute role, overwrite permissions will automatically be applied to every channel on this guild, preventing that role from sending messages, speaking, or adding reactions.")]
        [RequirePermRole]
        private async Task GetOrSetMuteRoleAsync([Remainder] string role = null)
        {
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(role))
            {
                if (Context.Guild.Roles.FirstOrDefault(x => x.Id == (ulong) gc.MuteRole) is SocketRole r)
                {
                    await Context.Channel
                        .SendConfirmAsync($"This guild's mute role is currently set to role **{r.Name}** (`{r.Id}`).")
                        .ConfigureAwait(false);
                    return;
                }

                await Context.Channel.SendErrorAsync("This guild's mute role is currently not set, or was deleted.")
                    .ConfigureAwait(false);
                return;
            }

            if (Context.Guild.Roles.FirstOrDefault(x =>
                x.Name.Equals(role, StringComparison.InvariantCultureIgnoreCase)
                || ulong.TryParse(role, out var result) && x.Id == result) is SocketRole ro)
            {
                gc.MuteRole = (long) ro.Id;
                await _db.UpdateAsync(gc).ConfigureAwait(false);
                await Context.Channel
                    .SendConfirmAsync($"This guild's mute role has been set to role **{ro.Name}** (`{ro.Id}`).")
                    .ConfigureAwait(false);
                return;
            }

            await Context.Channel.SendErrorAsync("Could not find a role by that name or ID to set as the mute role.")
                .ConfigureAwait(false);
        }

        [Command("lookingtoplayrole")]
        [Alias("ltprole")]
        [Summary("Gets or sets this guild's looking to play role. Users who use {p}ltp will gain this role.")]
        [Usage("{p}ltprole Looking to Play")]
        [Remarks("To modify the number of hours this role lasts, utilize the {p}ltphours command.")]
        [RequirePermRole]
        private async Task GetOrSetLtpRoleAsync([Remainder] string role = null)
        {
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(role))
            {
                if (Context.Guild.Roles.FirstOrDefault(x => x.Id == (ulong) gc.LookingToPlayRole) is SocketRole r)
                {
                    await Context.Channel
                        .SendConfirmAsync($"This guild's looking to play role is currently set to role **{r.Name}** (`{r.Id}`).")
                        .ConfigureAwait(false);
                    return;
                }

                await Context.Channel.SendErrorAsync("This guild's looking to play role is currently not set, or was deleted.")
                    .ConfigureAwait(false);
                return;
            }

            if (Context.Guild.Roles.FirstOrDefault(x =>
                x.Name.Equals(role, StringComparison.InvariantCultureIgnoreCase)
                || ulong.TryParse(role, out var result) && x.Id == result) is SocketRole ro)
            {
                gc.LookingToPlayRole = (long) ro.Id;
                await _db.UpdateAsync(gc).ConfigureAwait(false);
                await Context.Channel
                    .SendConfirmAsync($"This guild's looking to play role has been set to role **{ro.Name}** (`{ro.Id}`).")
                    .ConfigureAwait(false);
                return;
            }

            await Context.Channel.SendErrorAsync("Could not find a role by that name or ID to set as the looking to play role.")
                .ConfigureAwait(false);
        }

        [Command("lookingtoplayhours")]
        [Alias("ltphours")]
        [Summary(
            "Gets or sets this guild's maximum looking to play hours. To disable, simply supply a 0 for the timeout.")]
        [Usage("{p}ltphours 6")]
        [Remarks("Note: by default, the looking to play role does not expire (defaults to 0).")]
        [RequirePermRole]
        private async Task GetOrSetLtpHours(long hours = long.MinValue)
        {
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (hours == long.MinValue || hours == gc.LookingToPlayMaxHours)
            {
                await Context.Channel
                    .SendConfirmAsync(
                        $"This guild's looking to play timeout is currently {(gc.LookingToPlayMaxHours == 0 ? "disabled (set to 0)." : $"set to {gc.LookingToPlayMaxHours} hour(s).")}")
                    .ConfigureAwait(false);
                return;
            }

            if (hours < 0)
            {
                await Context.Channel
                    .SendErrorAsync("Please supply a non-negative number for the maximum hours, or 0 to disable it.")
                    .ConfigureAwait(false);
                return;
            }

            gc.LookingToPlayMaxHours = hours;
            await _db.UpdateAsync(gc).ConfigureAwait(false);
            await Context.Channel
                .SendConfirmAsync(
                    $"This guild's looking to play timeout has been {(hours == 0 ? "disabled." : $"set to {hours} hours.")}")
                .ConfigureAwait(false);
        }

        [Command("upvotearrow")]
        [Summary(
            "Gets or sets this guild's upvote arrow. Suggestions and the {p}vote command utilize this emote. Defaults to ⬆.")]
        [Usage("{p}upvotearrow ⬆")]
        [RequirePermRole]
        private async Task GetOrSetUpvoteArrowAsync(string emoteStr)
        {
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(emoteStr) || emoteStr.Equals(gc.UpvoteArrow, StringComparison.InvariantCultureIgnoreCase))
            {
                await Context.Channel
                    .SendConfirmAsync($"This guild's upvote arrow is currently set to {gc.UpvoteArrow}.")
                    .ConfigureAwait(false);
                return;
            }

            var emote = Emote.TryParse(emoteStr, out var result) ? result : (IEmote) new Emoji(emoteStr);

            try
            {
                await Context.Message.AddReactionAsync(emote).ConfigureAwait(false);
                gc.UpvoteArrow = emote.ToString();
                await _db.UpdateAsync(gc).ConfigureAwait(false);
                await Context.Channel.SendConfirmAsync($"This guild's upvote arrow has been set to {emote}.")
                    .ConfigureAwait(false);
            }
            catch
            {
                await Context.Channel
                    .SendErrorAsync("Could not set this guild's upvote arrow - either the input emote was malformed or I do not have access to use it.")
                    .ConfigureAwait(false);
                throw new ArgumentException("Could not parse upvote arrow emote.");
            }
        }

        [Command("downvotearrow")]
        [Summary("Gets or sets this guild's upvote arrow. Suggestions and the {p} vote command utilize this emote. Defaults to ⬇.")]
        [Usage("{p}downvote arrow ")]
        [RequirePermRole]
        private async Task GetOrSetDownvoteArrowAsync(string emoteStr)
        {
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(emoteStr) || emoteStr.Equals(gc.DownvoteArrow, StringComparison.InvariantCultureIgnoreCase))
            {
                await Context.Channel
                    .SendConfirmAsync($"This guild's downvote arrow is currently set to {gc.DownvoteArrow}.")
                    .ConfigureAwait(false);
                return;
            }

            var emote = Emote.TryParse(emoteStr, out var result) ? result : (IEmote) new Emoji(emoteStr);

            try
            {
                await Context.Message.AddReactionAsync(emote).ConfigureAwait(false);
                gc.DownvoteArrow = emote.ToString();
                await _db.UpdateAsync(gc).ConfigureAwait(false);
                await Context.Channel.SendConfirmAsync($"This guild's downvote arrow has been set to {emote}.")
                    .ConfigureAwait(false);
            }
            catch
            {
                await Context.Channel
                    .SendErrorAsync("Could not set this guild's downvote arrow - either the input emote was malformed or I do not have access to use it.")
                    .ConfigureAwait(false);
                throw new ArgumentException("Could not parse downvote arrow emote.");
            }
        }

        [Command("verboseerrors")]
        [Alias("ve")]
        [Summary(
            "Gets or sets this guild's error display mode.\n`true` will show all command errors except \"Unknown command.\".\n`false` will only show pre-defined errors from within the command.")]
        [Usage("{p}ve true")]
        [Remarks("This setting defaults to false.")]
        [RequirePermRole]
        private async Task GetOrSetVerboseErrorsAsync(bool? flag = null)
        {
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (flag is null || flag == gc.VerboseErrors)
            {
                await Context.Channel
                    .SendConfirmAsync(
                        $"This guild is currently configured to {(gc.VerboseErrors ? string.Empty : "not ")}display verbose errors.")
                    .ConfigureAwait(false);
                return;
            }

            gc.VerboseErrors = (bool) flag;
            await _db.UpdateAsync(gc).ConfigureAwait(false);
            await Context.Channel
                .SendConfirmAsync(
                    $"This guild has been configured to {(flag.Value ? string.Empty : "not ")}display verbose errors.")
                .ConfigureAwait(false);
        }

        [Command("greetusers")]
        [Summary("Gets or sets this guild's greeting mode.\n`true` will greet users if the guild has its greeting channel set up.\n`false` will disable greeting users.")]
        [Usage("{p}greetusers true")]
        [RequirePermRole]
        private async Task GetOrSetGreetingModeAsync(bool? flag = null)
        {
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (flag is null || flag == gc.GreetUserOnJoin)
            {
                await Context.Channel
                    .SendConfirmAsync(
                        $"This guild is currently configured to {(gc.GreetUserOnJoin ? string.Empty : "not ")}greet users on joining.")
                    .ConfigureAwait(false);
                return;
            }

            gc.GreetUserOnJoin = (bool) flag;
            await _db.UpdateAsync(gc).ConfigureAwait(false);
            await Context.Channel
                .SendConfirmAsync(
                    $"This guild has been configured to {(flag.Value ? string.Empty : "not ")}greet users on joining.")
                .ConfigureAwait(false);
        }

        [Command("greetmessage")]
        [Alias("greetmsg")]
        [Summary("Gets or sets this guild's greet message. Supports TOML embeds.\n" +
                    "You may use `{user}` to mention the user you are greeting.")]
        [Usage("{p}greetmsg Welcome to the server, {user}!")]
        [RequirePermRole]
        private async Task GetOrSetGreetMessageAsync([Remainder] string greetMsgOrEmbed = null)
        {
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(greetMsgOrEmbed))
            {
                try
                {
                    var a = TomlEmbedBuilder.ReadToml(gc.GreetMessage.Replace("{user}", Context.User.Mention).Trim());
                    if (a is TomlEmbed e)
                    {
                        await Context.Channel.EmbedAsync(e).ConfigureAwait(false);
                    }
                }
                catch
                {
                    await Context.Channel.SendMessageAsync(gc.GreetMessage.Replace("{user}", Context.User.Mention)).ConfigureAwait(false);
                }

                return;
            }

            gc.GreetMessage = greetMsgOrEmbed;
            await _db.UpdateAsync(gc).ConfigureAwait(false);

            await Context.Channel
                .SendConfirmAsync(
                    $"This guild's greet message has been updated. Use `{Config.BotPrefix}greetmsg` to view it.")
                .ConfigureAwait(false);
        }

        [Command("greettimeout")]
        [Summary(
            "Gets or sets this guild's greeting timeout, in seconds. The greeting message will be automatically deleted after that many seconds.\nSet to 0 to disable.")]
        [Usage("{p}greetseconds 60")]
        [RequirePermRole]
        private async Task GetOrSetGreetTimeoutAsync(long seconds = long.MinValue)
        {
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (seconds == long.MinValue || seconds == gc.GreetTimeout)
            {
                await Context.Channel
                    .SendConfirmAsync(
                        $"This guild's greeting timeout is currently {(gc.GreetTimeout == 0 ? "disabled." : $"set to {gc.GreetTimeout} seconds.")}")
                    .ConfigureAwait(false);
                return;
            }

            if (seconds < 0)
            {
                await Context.Channel
                    .SendErrorAsync("Please supply a non-negative number for the timeout, or 0 to disable.")
                    .ConfigureAwait(false);
                return;
            }

            gc.GreetTimeout = seconds;
            await _db.UpdateAsync(gc).ConfigureAwait(false);

            await Context.Channel
                .SendConfirmAsync(
                    $"This guild's greeting timeout has been {(seconds == 0 ? "disabled." : $"set to {seconds} seconds.")}")
                .ConfigureAwait(false);
        }

        [Command("enablerespects")]
        [Summary("Gets or sets this guild's respects functionality. Sending `F` in any channel will increment a respects counter, once per day per user.\n" +
                    "`true` enables this functionality.\n" +
                    "`false` disables it.")]
        [Usage("{p}enablerespects true")]
        [RequirePermRole]
        private async Task GetOrSetRespectsAsync(bool? flag = null)
        {
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (flag is null || flag == gc.EnableRespects)
            {
                await Context.Channel
                    .SendConfirmAsync(
                        $"This guild's respects counter is currently {(gc.EnableRespects ? "enabled" : "disabled")}.")
                    .ConfigureAwait(false);
                return;
            }

            gc.EnableRespects = (bool) flag;
            await _db.UpdateAsync(gc).ConfigureAwait(false);
            await Context.Channel
                .SendConfirmAsync(
                    $"This guild's respects counter has been {(gc.EnableRespects ? "enabled" : "disabled")}.")
                .ConfigureAwait(false);
        }

        [Command("invitefiltering")]
        [Summary("Gets or sets this guild's invite filtering functionality. Any discord invite links posted that are not invites to this guild will be filtered by the bot and deleted.\n" +
                    "`true` enables this functionality.\n" +
                    "`false` disables it.")]
        [Usage("{p}invitefiltering true")]
        [Remarks("Note: This bot requires ManageGuild permissions to access invites to grant itself a \"whitelist\". Otherwise, it will delete all invites, not just external ones.")]
        [RequirePermRole]
        private async Task GetOrSetInviteFilteringAsync(bool? flag = null)
        {
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (flag is null || flag == gc.EnableRespects)
            {
                await Context.Channel
                    .SendConfirmAsync(
                        $"Invite filtering on this guild is currently {(gc.GreetUserOnJoin ? "enabled" : "disabled")}.")
                    .ConfigureAwait(false);
                return;
            }

            gc.InviteFiltering = (bool) flag;
            await _db.UpdateAsync(gc).ConfigureAwait(false);
            await Context.Channel
                .SendConfirmAsync(
                    $"Invite filtering on this guild has been {(gc.GreetUserOnJoin ? "enabled" : "disabled")}.")
                .ConfigureAwait(false);
        }

        [Command("phraseminimumlength")]
        [Alias("phraseminlength", "pml")]
        [Summary("Gets or sets this guild's minimum phrase length, in characters. Phrases **shorter** than this number will not be created. Set to 0 to disable.")]
        [Usage("{p}phraseminlength 3")]
        [RequirePermRole]
        private async Task GetOrSetPhraseMinLengthAsync(long length = long.MinValue)
        {
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (length == long.MinValue || length == gc.PhraseMinLength)
            {
                await Context.Channel
                    .SendConfirmAsync(
                        $"This guild's minimum phrase length is currently set to {gc.PhraseMinLength} characters.")
                    .ConfigureAwait(false);
                return;
            }

            if (length < 0)
            {
                await Context.Channel
                    .SendErrorAsync(
                        "Please supply a non-negative number for minimum phrase length, or 0 to disable it.")
                    .ConfigureAwait(false);
                return;
            }

            gc.PhraseMinLength = length;
            await _db.UpdateAsync(gc).ConfigureAwait(false);

            await Context.Channel
                .SendConfirmAsync($"This guild's minimum phrase length has been set to {length} characters.")
                .ConfigureAwait(false);
        }

        [Command("logchannel")]
        [Summary("Get or set this guild's log channel. User leave/join/ban/unban/kick and message deletions will be logged to this channel.")]
        [Usage("{p}logchannel #logs")]
        [RequirePermRole]
        private async Task GetOrSetLogChannelAsync(IGuildChannel channel = null)
        {
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (channel is null)
            {
                if (Context.Guild.GetTextChannel((ulong) gc.LogChannel) is SocketTextChannel ch)
                {
                    await Context.Channel
                        .SendConfirmAsync($"This guild's log channel is currently set to channel {ch.Mention} (`{ch.Id}`)")
                        .ConfigureAwait(false);
                    return;
                }

                await Context.Channel
                    .SendErrorAsync("This guild's log channel is currently not set, or was deleted.")
                    .ConfigureAwait(false);
                return;
            }

            if (channel.Guild.Id != Context.Guild.Id || !(channel is SocketTextChannel c)) return;

            gc.LogChannel = (long) c.Id;
            await _db.UpdateAsync(gc).ConfigureAwait(false);

            await Context.Channel
                .SendConfirmAsync($"This guild's log channel has been set to channel {c.Mention} (`{c.Id}`)")
                .ConfigureAwait(false);
        }

        [Command("suggestionchannel")]
        [Summary("Get or set this guild's suggestion channel. Users who suggest with {p}suggest will have their suggestions posted to this channel.")]
        [Usage("{p}suggestionchannel #suggestions")]
        [RequirePermRole]
        private async Task GetOrSetSuggestionChannelAsync(IGuildChannel channel = null)
        {
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (channel is null)
            {
                if (Context.Guild.GetTextChannel((ulong) gc.LogChannel) is SocketTextChannel ch)
                {
                    await Context.Channel
                        .SendConfirmAsync($"This guild's suggestion channel is currently set to channel {ch.Mention} (`{ch.Id}`)")
                        .ConfigureAwait(false);
                    return;
                }

                await Context.Channel
                    .SendErrorAsync("This guild's suggestion channel is currently not set, or was deleted.")
                    .ConfigureAwait(false);
                return;
            }

            if (channel.Guild.Id != Context.Guild.Id || !(channel is SocketTextChannel c)) return;

            gc.SuggestionChannel = (long) c.Id;
            await _db.UpdateAsync(gc).ConfigureAwait(false);

            await Context.Channel
                .SendConfirmAsync($"This guild's suggestion channel has been set to channel {c.Mention} (`{c.Id}`)")
                .ConfigureAwait(false);
        }

        [Command("suggestionarchive")]
        [Summary("Get or set this guild's suggestion archive channel. Approved or denied suggestions will be posted to this channel.")]
        [Usage("{p}suggestionarchive #suggestionarchive")]
        [Remarks("Note: if you do not set a suggestion archive channel, the !!suggestion command will not function.")]
        [RequirePermRole]
        private async Task GetOrSetSuggestionArchiveAsync(IGuildChannel channel = null)
        {
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (channel is null)
            {
                if (Context.Guild.GetTextChannel((ulong) gc.LogChannel) is SocketTextChannel ch)
                {
                    await Context.Channel
                        .SendConfirmAsync($"This guild's suggestion archive channel is currently set to channel {ch.Mention} (`{ch.Id}`)")
                        .ConfigureAwait(false);
                    return;
                }

                await Context.Channel
                    .SendErrorAsync("This guild's suggestion archive channel is currently not set, or was deleted.")
                    .ConfigureAwait(false);
                return;
            }

            if (channel.Guild.Id != Context.Guild.Id || !(channel is SocketTextChannel c)) return;

            gc.SuggestionArchive = (long) c.Id;
            await _db.UpdateAsync(gc).ConfigureAwait(false);

            await Context.Channel
                .SendConfirmAsync($"This guild's suggestion archive channel has been set to channel {c.Mention} (`{c.Id}`)")
                .ConfigureAwait(false);
        }

        [Command("greetchannel")]
        [Summary("Get or set this guild's greeting channel. Users who join will trigger a greeting message.")]
        [Usage("{p}greetchannel #welcome")]
        [RequirePermRole]
        private async Task GetOrSetGreetChannelAsync(IGuildChannel channel = null)
        {
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (channel is null)
            {
                if (Context.Guild.GetTextChannel((ulong) gc.LogChannel) is SocketTextChannel ch)
                {
                    await Context.Channel
                        .SendConfirmAsync($"This guild's greet channel is currently set to channel {ch.Mention} (`{ch.Id}`)")
                        .ConfigureAwait(false);
                    return;
                }

                await Context.Channel
                    .SendErrorAsync("This guild's greet channel is currently not set, or was deleted.")
                    .ConfigureAwait(false);
                return;
            }

            if (channel.Guild.Id != Context.Guild.Id || !(channel is SocketTextChannel c)) return;

            gc.GreetChannel = (long) c.Id;
            await _db.UpdateAsync(gc).ConfigureAwait(false);

            await Context.Channel
                .SendConfirmAsync($"This guild's greet channel has been set to channel {c.Mention} (`{c.Id}`)")
                .ConfigureAwait(false);
        }
    }
}