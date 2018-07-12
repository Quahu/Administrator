using System;
using System.Linq;
using System.Threading.Tasks;
using Administrator.Common;
using Administrator.Common.Attributes;
using Administrator.Common.Database;
using Administrator.Extensions;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NLog.Fluent;

namespace Administrator.Modules.Configuration
{
    [Name("Configuration")]
    [RequireContext(ContextType.Guild)]
    [RequirePermissionsPass]
    public class ConfigurationCommands : AdminBase
    {
        private enum LogType
        {
            // All,
            Warn,
            Appeal,
            Mute,
            Join,
            Leave,
            Ban,
            Unban,
            MessageDelete,
            MessageUpdate
        }

        [Command("resetconfig", RunMode = RunMode.Async)]
        [Summary("Reset the guild config to all default values.\n" +
                 "Only usable by the guild owner.")]
        [Usage("resetconfig")]
        [RequireGuildOwner]
        private async Task<RuntimeResult> ResetConfigAsync()
        {
            var resetCode = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Substring(0, 8).ToLower();
            var msg = await SendErrorAsync(
                "**WARNING**: You are about to reset all guild configurations to their default values.\n" +
                $"If you're certain you want to do this, type `{resetCode}`.");
            var response = await GetNextMessageAsync(NextMessageCriteria.ChannelUser);
            if (response?.Content.Equals(resetCode) == true)
            {
                using (var ctx = new AdminContext())
                {
                    var gc = ctx.GetOrCreateGuildConfig(Context.Guild);
                    ctx.Remove(gc);
                    ctx.SaveChanges();
                    await msg.ModifyAsync(x => x.Embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithDescription("Guild config has been reset.")
                        .Build());
                    return await CommandSuccess();
                }
            }

            await msg.ModifyAsync(x => x.Embed = new EmbedBuilder()
                .WithErrorColor()
                .WithDescription("Response timeout reached, or your response did not match the reset code.")
                .Build());
            return await CommandError("Message timed out or was not a match.");
        }

        [Command("permrole")]
        [Summary("Sets the guild's permrole.\n" +
                 "Supply no arguments to view the current permrole.")]
        [Usage("permrole", "permrole Moderator")]
        [RequireGuildOwner]
        private Task<RuntimeResult> GetOrSetPermRole([Remainder] string roleName)
        {
            if (!(Context.Guild.Roles.FirstOrDefault(x => x.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase)) is
                SocketRole newPermRole))
            {
                return CommandError("New permrole not found.",
                    "Could not find a role by that name to set as the permrole.");
            }

            var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
            gc.PermRoleId = newPermRole.Id;
            DbContext.Update(gc);
            DbContext.SaveChanges();
            return CommandSuccess($"Permrole has been updated to role **{newPermRole.Name}** (`{newPermRole.Id}`).");
        }

        [Command("permrole")]
        [Summary("Sets the guild's permrole.\n" +
                 "Supply no arguments to view the current permrole.")]
        [Usage("permrole", "permrole Moderator")]
        [RequireGuildOwner]
        private Task<RuntimeResult> GetOrSetPermRole()
        {
            var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
            if (!gc.PermRoleId.HasValue)
            {
                return CommandError("Permrole not set.",
                    "This guild's permrole has not been set up yet.");
            }

            if (!(Context.Guild.GetRole(gc.PermRoleId.Value) is SocketRole permRole))
            {
                return CommandError("Permrole not found or deleted.",
                    "This guild's permrole does not exist or was deleted.");
            }

            return CommandSuccess($"This guild's permrole is currently role **{permRole.Name}** (`{permRole.Id}`).");
        }

        [RequirePermRole]
        public class ConfigurationSubmodule : ConfigurationCommands
        {
            [Command("showconfig")]
            [Alias("config")]
            [Summary("Show this guild's configuration.")]
            [Usage("showconfig")]
            private Task<RuntimeResult> ShowGuildConfig()
            {
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                return CommandSuccess(embed: new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle($"Guild configuration for {Context.Guild.Name}")
                    .WithThumbnailUrl(Context.Guild.IconUrl)
                    .AddField("General",
                        $"**Prefix**: \"{DbContext.GetPrefixOrDefault(Context.Guild)}\"\n" +
                        $"**Permrole**: {Context.Guild.GetRole(gc.PermRoleId.GetValueOrDefault())?.Name ?? "Not set"}\n" + // shouldn't happen?
                        $"**Mute role**: {Context.Guild.GetRole(gc.MuteRoleId)?.Name ?? "Not set"} ({gc.MuteRole.ToString().ToLower()}d)\n" +
                        $"**Looking to Play role**: {Context.Guild.GetRole(gc.LtpRoleId)?.Name ?? "Not set"} ({gc.LtpRole.ToString().ToLower()}d)\n" +
                        $"**Looking to Play timeout**: {(gc.LtpRoleTimeout is TimeSpan ts ? StringExtensions.FormatTimeSpan(ts) : "Disabled")}\n" +
                        $"**Upvote arrow**: {gc.UpvoteArrow}\n" +
                        $"**Downvote arrow**: {gc.DownvoteArrow}\n" +
                        $"**Respects**: {gc.TrackRespects.ToString().ToLower()}d\n" +
                        $"**Invite filtering**: {gc.FilterInvites.ToString().ToLower()}d\n" +
                        $"**Verbose errors**: {gc.VerboseErrors.ToString().ToLower()}d\n" +
                        $"**Invite code**: {gc.InviteCode}", true)
                    .AddField("Logging channels",
                        $"**Warn**: {Context.Guild.GetTextChannel(gc.LogWarnChannelId)?.Mention ?? "Not set"}\n" +
                        $"**Appeal**: {Context.Guild.GetTextChannel(gc.LogAppealChannelId)?.Mention ?? "Not set"}\n" +
                        $"**Mute**: {Context.Guild.GetTextChannel(gc.LogMuteChannelId)?.Mention ?? "Not set"}\n" +
                        $"**Join**: {Context.Guild.GetTextChannel(gc.LogJoinChannelId)?.Mention ?? "Not set"}\n" +
                        $"**Leave**: {Context.Guild.GetTextChannel(gc.LogLeaveChannelId)?.Mention ?? "Not set"}\n" +
                        $"**Ban**: {Context.Guild.GetTextChannel(gc.LogBanChannelId)?.Mention ?? "Not set"}\n" +
                        $"**Unban**: {Context.Guild.GetTextChannel(gc.LogUnbanChannelId)?.Mention ?? "Not set"}\n" +
                        $"**MessageDelete**: {Context.Guild.GetTextChannel(gc.LogMessageDeletionChannelId)?.Mention ?? "Not set"}\n" +
                        $"**MessagedUpdate**: {Context.Guild.GetTextChannel(gc.LogMessageUpdatedChannelId)?.Mention ?? "Not set"}", true)
                    .AddField("Suggestions",
                        $"**Functionality**: {gc.Suggestions.ToString().ToLower()}d\n" +
                        $"**Suggestion channel**: {Context.Guild.GetTextChannel(gc.SuggestionChannelId)?.Mention ?? "Not set"}\n" +
                        $"**Archiving functionality**: {gc.ArchiveSuggestions.ToString().ToLower()}d\n" +
                        $"**Archive channel**: {Context.Guild.GetTextChannel(gc.SuggestionArchiveId)?.Mention ?? "Not set"}", true)
                    .AddField("Greetings",
                        $"**Functionality**: {gc.Greetings.ToString().ToLower()}d\n" +
                        $"**Greeting channel**: {Context.Guild.GetTextChannel(gc.GreetChannelId)?.Mention ?? "Not set"}\n" +
                        $"**Greeting message**: Use `{DbContext.GetPrefixOrDefault(Context.Guild)}greetmessage` to view\n" +
                        $"**Greeting timeout**: {(gc.GreetTimeout is TimeSpan ts1 ? StringExtensions.FormatTimeSpan(ts1) : "Disabled")}", true)
                    .Build());
            }

            [Command("prefix")]
            [Summary("Gets or sets the guild's prefix.\n" +
                     "Use the default prefix `{defaultprefix}` to see the guild's prefix if the prefix is unknown.")]
            [Usage("prefix", "prefix .")]
            [Remarks("Prefixes are limited to {prefixmaxlength} characters in length. If you wish to have (trailing) space in your prefix, wrap it in \"quotes \".")]
            [RequirePermRole]
            private Task<RuntimeResult> GetOrSetPrefix(string newPrefix)
            {
                if (string.IsNullOrWhiteSpace(newPrefix)) return CommandError("Prefix cannot be null or only whitespace.");

                if (newPrefix.Length > BotConfig.PREFIX_MAX_LENGTH)
                {
                    return CommandError($"Prefix longer than {BotConfig.PREFIX_MAX_LENGTH}.",
                        $"Prefix may not be any longer than {BotConfig.PREFIX_MAX_LENGTH} characters.");
                }

                newPrefix = newPrefix.Replace("\n", string.Empty);

                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                gc.Prefix = newPrefix;
                DbContext.Update(gc);
                DbContext.SaveChanges();
                return CommandSuccess($"Prefix on this guild has been changed to \"{newPrefix}\".");
            }

            [Command("prefix")]
            [Summary("Gets or sets the guild's prefix.\n " +
                     "Use the default prefix `{defaultprefix}` to see the guild's prefix if the prefix is unknown.")]
            [Usage("prefix", "prefix .")]
            [Remarks("Prefixes are limited to {prefixmaxlength} characters in length. If you wish to have (trailing) space in your prefix, wrap it in \"quotes \".")]
            private Task<RuntimeResult> GetOrSetPrefix()
                => CommandSuccess($"Prefix on this guild is \"{DbContext.GetPrefixOrDefault(Context.Guild)}\".");

            [Command("muterole", RunMode = RunMode.Async)]
            [Summary("Gets or sets the guild's mute role.\n\n" +
                     "Supply no arguments to view the current mute role (if set).\n" +
                     "Use `{prefix}muterole [enable/disable]` to enable or disable the functionality.\n" +
                     "Use `{prefix}mute` to automatically apply this role to a user.")]
            [Usage("muterole Silenced", "muterole disable")]
            [Priority(1)]
            private async Task<RuntimeResult> GetOrSetMuteRoleAsync(Functionality mode)
            {
                using (var ctx = new AdminContext())
                {
                    var gc = ctx.GetOrCreateGuildConfig(Context.Guild);
                    gc.MuteRole = mode;
                    ctx.Update(gc);
                    ctx.SaveChanges();
                    if (Context.Guild.GetRole(gc.MuteRoleId) is SocketRole muteRole)
                    {
                        var msg = await SendOkAsync(
                            $"Mute role functionality on this guild is now **{mode.ToString().ToLower()}d**.\n" +
                             "Currently making changes to affected text and voice channels. Please wait.");
                        var voiceChannelOverwrites = new OverwritePermissions(speak: PermValue.Deny, useVoiceActivation: PermValue.Deny);
                        var textChannelOverwrites = new OverwritePermissions(sendMessages: PermValue.Deny,
                            sendTTSMessages: PermValue.Deny, attachFiles: PermValue.Deny, addReactions: PermValue.Deny,
                            useExternalEmojis: PermValue.Deny);

                        foreach (var textChannel in Context.Guild.TextChannels)
                        {
                            try
                            {
                                switch (mode)
                                {
                                    case Functionality.Disable:
                                        await textChannel.RemovePermissionOverwriteAsync(muteRole);
                                        break;
                                    case Functionality.Enable:
                                        await textChannel.AddPermissionOverwriteAsync(muteRole, textChannelOverwrites);
                                        break;
                                }
                            }
                            catch
                            {
                                // ignored
                            }
                        }

                        foreach (var voiceChannel in Context.Guild.VoiceChannels)
                        {
                            try
                            {
                                switch (mode)
                                {
                                    case Functionality.Disable:
                                    await voiceChannel.RemovePermissionOverwriteAsync(muteRole);
                                    break;
                                    case Functionality.Enable:
                                    await voiceChannel.AddPermissionOverwriteAsync(muteRole, voiceChannelOverwrites);
                                    break;
                                }
                            }
                            catch
                            {
                                // ignored
                            }
                        }

                        await msg.ModifyAsync(x => x.Embed = new EmbedBuilder()
                            .WithOkColor()
                            .WithDescription("Text and voice channels have been updated.")
                            .Build());
                        return await CommandSuccess();
                    }
                    return await CommandSuccess($"Mute role functionality on this guild is now **{mode.ToString().ToLower()}d**.");
                }
            }

            [Command("muterole")]
            [Summary("Gets or sets the guild's mute role.\n\n" +
                     "Supply no arguments to view the current mute role (if set).\n" +
                     "Use `{prefix}muterole [enable/disable]` to enable or disable this functionality.\n" +
                     "Use `{prefix}mute` to automatically apply this role to a user.")]
            [Usage("muterole Silenced", "muterole disable")]
            [Priority(0)]
            private Task<RuntimeResult> GetOrSetMuteRole([Remainder] string roleName)
            {
                if (!(Context.Guild.Roles.FirstOrDefault(x => x.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase)) is
                    SocketRole newMuteRole))
                {
                    return CommandError("New mute role not found.",
                        "Could not find a role by that name to set as the mute role.");
                }

                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                gc.MuteRoleId = newMuteRole.Id;
                DbContext.Update(gc);
                DbContext.SaveChanges();
                return CommandSuccess($"Mute role on this guild has been updated to role **{newMuteRole.Name}** (`{newMuteRole.Id}`).\n" +
                                      $"Note: mute role functionality is currently **{gc.MuteRole.ToString().ToLower()}d**.\n" +
                                      $"Use `{DbContext.GetPrefixOrDefault(Context.Guild)}muterole enable/disable` to modify it.");
            }

            [Command("muterole")]
            [Summary("Gets or sets the guild's mute role.\n\n" +
                     "Supply no arguments to view the current mute role (if set).\n" +
                     "Use `{prefix}muterole [enable/disable]` to enable or disable this functionality.\n" +
                     "Use `{prefix}mute` to automatically apply this role to a user.")]
            [Usage("muterole Silenced", "muterole disable")]
            private Task<RuntimeResult> GetOrSetMuteRole()
            {
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                var s = "Mute role for this guild is currently not set up.\n" +
                        $"Mute role functionality is currently **{gc.MuteRole.ToString().ToLower()}d**.";
                if (Context.Guild.GetRole(gc.MuteRoleId) is SocketRole muteRole)
                {
                    s = s.Replace("not set up", $"**{muteRole.Name}** (`{muteRole.Id}`)");
                }

                return CommandSuccess(s);
            }

            [Command("ltprole")]
            [Summary("Gets or sets the guild's Looking to Play role.\n\n" +
                     "Supply no arguments to view the current LTP role (if set).\n" +
                     "Use `{prefix}ltprole [enable/disable]` to enable or disable this functionality.\n" +
                     "Use `{prefix}ltp` to automatically apply this role to yourself.")]
            [Usage("ltprole Looking to Play", "ltprole disable")]
            [Priority(1)]
            private Task<RuntimeResult> GetOrSetLtpRole(Functionality mode)
            {
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                gc.LtpRole = mode;
                DbContext.Update(gc);
                DbContext.SaveChanges();
                return CommandSuccess($"Looking to Play role functionality on this guild is now **{mode.ToString().ToLower()}d**.");
            }

            [Command("ltprole")]
            [Summary("Gets or sets the guild's Looking to Play role.\n\n" +
                     "Supply no arguments to view the current LTP role (if set).\n" +
                     "Use `{prefix}ltprole [enable/disable]` to enable or disable this functionality.\n" +
                     "Use `{prefix}ltp` to automatically apply this role to yourself.")]
            [Usage("ltprole Looking to Play", "ltprole disable")]
            [Priority(0)]
            private Task<RuntimeResult> GetOrSetLtpRole([Remainder] string roleName)
            {
                if (!(Context.Guild.Roles.FirstOrDefault(x => x.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase)) is
                    SocketRole newLtpRole))
                {
                    return CommandError("New LTP role not found.",
                        "Could not find a role by that name to set as the Looking to Play role.");
                }

                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                gc.LtpRoleId = newLtpRole.Id;
                DbContext.Update(gc);
                DbContext.SaveChanges();
                return CommandSuccess($"Looking to Play role on this guild has been updated to role **{newLtpRole.Name}** (`{newLtpRole.Id}`).\n" +
                                      $"Note: Looking to Play role functionality is currently **{gc.LtpRole.ToString().ToLower()}d**.");
            }

            [Command("ltprole")]
            [Summary("Gets or sets the guild's Looking to Play role.\n\n" +
                     "Supply no arguments to view the current LTP role (if set).\n" +
                     "Use `{prefix}ltprole [enable/disable]` to enable or disable this functionality.\n" +
                     "Use `{prefix}ltp` to automatically apply this role to yourself.")]
            [Usage("ltprole Looking to Play", "ltprole disable")]
            private Task<RuntimeResult> GetOrSetLtpRole()
            {
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                var s = "Looking to Play role on this guild is currently not set up.\n" +
                        $"Looking to Play role functionality is currently **{gc.LtpRole.ToString().ToLower()}d**.";
                if (Context.Guild.GetRole(gc.LtpRoleId) is SocketRole ltpRole)
                {
                    s = s.Replace("not set up", $"**{ltpRole.Name}** (`{ltpRole.Id}`)");
                }

                return CommandSuccess(s);
            }

            [Command("ltptimeout")]
            [Summary("Gets or set the guild's Looking to Play role timeout.\n\n" +
                     "Supply no arguments to view the current timeout.\n" +
                     "This timeout has a minimum of 1 minute, but you may specify `0` to disable it.")]
            [Usage("ltptimeout 0", "ltptimeout 4h")]
            [Remarks("Note: imeouts are checked every 30 seconds and may not be accurate to the second.")]
            [Priority(2)]
            private Task<RuntimeResult> GetOrSetLtpTimeout(TimeSpan timeout)
            {
                if (timeout < TimeSpan.FromMinutes(1))
                {
                    return CommandError("Timeout is smaller than 1 minute.",
                        "The timespan provided is shorter than 1 minute.");
                }

                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                gc.LtpRoleTimeout = timeout;
                DbContext.Update(gc);
                DbContext.SaveChanges();
                return CommandSuccess(
                    $"Looking to Play role timeout on this guild has been updated to **{StringExtensions.FormatTimeSpan(timeout)}**.");
            }

            [Command("ltptimeout")]
            [Summary("Gets or set the guild's Looking to Play role timeout.\n\n" +
                     "Supply no arguments to view the current timeout.\n" +
                     "This timeout has a minimum of 1 minute, but you may specify `0` to disable it.")]
            [Usage("ltptimeout 0", "ltptimeout 4h")]
            [Remarks("Note: imeouts are checked every 30 seconds and may not be accurate to the second.")]
            [Priority(1)]
            private Task<RuntimeResult> GetOrSetLtpTimeout(int mustBeZero)
            {
                if (mustBeZero != 0) return CommandError("Input was non-zero.");
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                gc.LtpRoleTimeout = null;
                DbContext.Update(gc);
                DbContext.SaveChanges();
                return CommandSuccess("Looking to Play role timeout on this guild has been **disabled**.");
            }

            [Command("ltptimeout")]
            [Summary("Gets or set the guild's Looking to Play role timeout.\n\n" +
                     "Supply no arguments to view the current timeout.\n" +
                     "This timeout has a minimum of 1 minute, but you may specify `0` to disable it.")]
            [Usage("ltptimeout 0", "ltptimeout 4h")]
            [Remarks("Note: imeouts are checked every 30 seconds and may not be accurate to the second.")]
            [Priority(0)]
            private Task<RuntimeResult> GetOrSetLtpTimeout()
            {
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                return CommandSuccess(
                    $"Looking to Play role timeout on this guild is currently **{(gc.LtpRoleTimeout is TimeSpan timeout ? StringExtensions.FormatTimeSpan(timeout) : "disabled")}**.");
            }

            [Command("log")]
            [Summary("Log an event in the current channel.\n\n" +
                     "If the current event is already being logged in this channel, it will be disabled.\n" +
                     "If the current event is being logged in another channel, it will be switched to this channel.\n" +
                     "Currently supported log types: ```\nAll\nWarn\nAppeal\nMute\nJoin\nLeave\nBan\nUnban\nMessageDelete\nMessageUpdate\n```")]
            [Usage("log appeal")]
            [Remarks(
                "Note: some commands require events to be logged to function correctly, notably Appeal, Ban, Mute, and Warn.")]
            private Task<RuntimeResult> SetLogChannel(LogType type)
            {
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                var isDisabled = false;
                switch (type)
                {
                    /*
                    case LogType.All:
                        gc.LogBanChannelId = Context.Channel.Id;
                        gc.LogUnbanChannelId = Context.Channel.Id;
                        gc.LogAppealChannelId = Context.Channel.Id;
                        gc.LogJoinChannelId = Context.Channel.Id;
                        gc.LogLeaveChannelId = Context.Channel.Id;
                        gc.LogMessageDeletionChannelId = Context.Channel.Id;
                        gc.LogMessageUpdatedChannelId = Context.Channel.Id;
                        gc.LogMuteChannelId = Context.Channel.Id;
                        gc.LogWarnChannelId = Context.Channel.Id;
                        break;
                        */
                    case LogType.Warn:
                        if (gc.LogWarnChannelId == Context.Channel.Id)
                        {
                            gc.LogWarnChannelId = 0;
                            isDisabled = true;
                        }
                        else gc.LogWarnChannelId = Context.Channel.Id;
                        break;
                    case LogType.Appeal:
                        if (gc.LogAppealChannelId == Context.Channel.Id)
                        {
                            gc.LogAppealChannelId = 0;
                            isDisabled = true;
                        }
                        else gc.LogAppealChannelId = Context.Channel.Id;
                        break;
                    case LogType.Mute:
                        if (gc.LogMuteChannelId == Context.Channel.Id)
                        {
                            gc.LogMuteChannelId = 0;
                            isDisabled = true;
                        }
                        else gc.LogMuteChannelId = Context.Channel.Id;
                        break;
                    case LogType.Join:
                        if (gc.LogJoinChannelId == Context.Channel.Id)
                        {
                            gc.LogJoinChannelId = 0;
                            isDisabled = true;
                        }
                        else gc.LogJoinChannelId = Context.Channel.Id;
                        break;
                    case LogType.Leave:
                        if (gc.LogLeaveChannelId == Context.Channel.Id)
                        {
                            gc.LogLeaveChannelId = 0;
                            isDisabled = true;
                        }
                        else gc.LogLeaveChannelId = Context.Channel.Id;
                        break;
                    case LogType.Ban:
                        if (gc.LogBanChannelId == Context.Channel.Id)
                        {
                            gc.LogBanChannelId = 0;
                            isDisabled = true;
                        }
                        else gc.LogBanChannelId = Context.Channel.Id;
                        break;
                    case LogType.Unban:
                        if (gc.LogUnbanChannelId == Context.Channel.Id)
                        {
                            gc.LogUnbanChannelId = 0;
                            isDisabled = true;
                        }
                        else gc.LogUnbanChannelId = Context.Channel.Id;
                        break;
                    case LogType.MessageDelete:
                        if (gc.LogMessageDeletionChannelId == Context.Channel.Id)
                        {
                            gc.LogMessageDeletionChannelId = 0;
                            isDisabled = true;
                        }
                        else gc.LogMessageDeletionChannelId = Context.Channel.Id;
                        break;
                    case LogType.MessageUpdate:
                        if (gc.LogMessageUpdatedChannelId == Context.Channel.Id)
                        {
                            gc.LogMessageUpdatedChannelId = 0;
                            isDisabled = true;
                        }
                        else gc.LogMessageUpdatedChannelId = Context.Channel.Id;
                        break;
                }

                DbContext.Update(gc);
                DbContext.SaveChanges();
                return CommandSuccess($"**{type}** events will now {(isDisabled ? "no longer " : string.Empty)}be logged in this channel.");
            }

            [Command("suggestions")]
            [Summary("Enable or disable suggestion functionality on this guild (accessible with the `{prefix}suggest` command).\n" +
                     "Supply no arguments to view the current status of it.")]
            [Usage("suggestions disable")]
            [Remarks("Note: this does not disable the \"suggest\" command, but it does prevent it from functioning.")]
            private Task<RuntimeResult> GetOrSetSuggestionFunctionality(Functionality mode)
            {
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                gc.Suggestions = mode;
                DbContext.Update(gc);
                DbContext.SaveChanges();
                return CommandSuccess(
                    $"Suggestion functionality on this guild has been **{mode.ToString().ToLower()}d**.");
            }

            [Command("suggestions")]
            [Summary("Enable or disable suggestion functionality on this guild (accessible with the `{prefix}suggest` command).\n" +
                     "Supply no arguments to view the current status of it.")]
            [Usage("suggestions disable")]
            [Remarks("Note: this does not disable the \"suggest\" command, but it does prevent it from functioning.")]
            private Task<RuntimeResult> GetOrSetSuggestionFunctionality()
                => CommandSuccess(
                    $"Suggestion functionality on this guild is currently **{DbContext.GetOrCreateGuildConfig(Context.Guild).Suggestions.ToString().ToLower()}d**.");

            [Command("suggestionchannel")]
            [Summary("Gets or sets the guild's suggestion channel.\n" +
                     "Supply no arguments to view the current channel (if set).\n" +
                     "Use `{prefix}suggest` to add a suggestion to that channel.")]
            [Usage("suggestionchannel #suggestions")]
            private Task<RuntimeResult> GetOrSetSuggestionChannel(ITextChannel channel)
            {
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                gc.SuggestionChannelId = channel.Id;
                DbContext.Update(gc);
                DbContext.SaveChanges();
                return CommandSuccess(
                    $"Suggestion channel for this guild has been updated to {channel.Mention} (`{channel.Id}`).");
            }

            [Command("suggestionchannel")]
            [Summary("Gets or sets the guild's suggestion channel.\n" +
                     "Supply no arguments to view the current channel (if set).\n" +
                     "Use `{prefix}suggest` to add a suggestion to that channel.")]
            [Usage("suggestionchannel #suggestions")]
            private Task<RuntimeResult> GetOrSetSuggestionChannel()
            {
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                if (Context.Guild.GetTextChannel(gc.SuggestionChannelId) is SocketTextChannel c)
                {
                    return CommandSuccess(
                        $"Suggestion channel for this guild is currently channel {c.Mention} (`{c.Id}`).");
                }

                return CommandError("Suggestion channel not found.", "Suggestion channel for this guild is either not set, or was deleted.");
            }

            [Command("archivesuggestions")]
            [Summary("Enable or disable suggestion archive functionality on this guild.\n" +
                     "Supply no arguments to view the current status of it.\n" +
                     "Use the `{prefix}suggestion [deny/approve]` to move a suggestion from your suggeestion channel to this channel.")]
            [Usage("archivesuggestions enable")]
            [Remarks(
                "Note: If this functionality is disabled, suggestions can still be approved or denied, but will simply be removed.")]
            private Task<RuntimeResult> GetOrSetSuggestionArchiveFunctionality(Functionality mode)
            {
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                gc.ArchiveSuggestions = mode;
                DbContext.Update(gc);
                DbContext.SaveChanges();
                return CommandSuccess(
                    $"Suggestion archiving functionality on this guild has been **{mode.ToString().ToLower()}d**.");
            }

            [Command("archivesuggestions")]
            [Summary("Enable or disable suggestion archive functionality on this guild.\n" +
                     "Supply no arguments to view the current status of it.\n" +
                     "Use the `{prefix}suggestion [deny/approve]` to move a suggestion from your suggeestion channel to this channel.")]
            [Usage("archivesuggestions enable")]
            [Remarks(
                "Note: If this functionality is disabled, suggestions can still be approved or denied, but will simply be removed.")]
            private Task<RuntimeResult> GetOrSetSuggestionArchiveFunctionality()
                => CommandSuccess(
                    $"Suggestion archiving functionality on this guild is currently **{DbContext.GetOrCreateGuildConfig(Context.Guild).ArchiveSuggestions.ToString().ToLower()}d**.");

            [Command("suggestionarchive")]
            [Summary("Gets or sets the guild's suggestion archive channel.\n" +
                     "Supply no arguments to view the current channel (if set).\n" +
                     "Once set, use `{prefix}archivesuggestions enable` to enable suggestion archiving.")]
            [Usage("suggestionarchive #suggestion-archive")]
            private Task<RuntimeResult> GetOrSetSuggestionArchiveChannel(ITextChannel channel)
            {
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                gc.SuggestionArchiveId = channel.Id;
                DbContext.Update(gc);
                DbContext.SaveChanges();
                return CommandSuccess(
                    $"Suggestion archive channel has been updated to {channel.Mention} (`{channel.Id}`).");
            }

            [Command("suggestionarchive")]
            [Summary("Gets or sets the guild's suggestion archive channel.\n" +
                     "Supply no arguments to view the current channel (if set).\n" +
                     "Once set, use `{prefix}archivesuggestions enable` to enable suggestion archiving.")]
            [Usage("suggestionarchive #suggestion-archive")]
            private Task<RuntimeResult> GetOrSetSuggestionArchiveChannel()
            {
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                if (Context.Guild.GetTextChannel(gc.SuggestionArchiveId) is SocketTextChannel c)
                {
                    return CommandSuccess(
                        $"Suggestion archive channel for this guild is currently channel {c.Mention} (`{c.Id}`).");
                }

                return CommandError("Suggestion archive channel not found.", "Suggestion archive channel for this guild is either not set, or was deleted.");
            }

            [Command("greetings")]
            [Summary("Enable or disable greeting functionality for this guild.\n" +
                     "Supply no arguments to view the current status of it.\n" +
                     "Use `{prefix}greetchannel` to set the greeting channel.\n" +
                     "Use `{prefix}greetmessage` to set the greeting message.\n" +
                     "Use `{prefix}greettimeout` to set the greeting timeout.")]
            [Usage("greetings enable")]
            private Task<RuntimeResult> SetGreetingFunctionality(Functionality mode)
            {
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                gc.Greetings = mode;
                DbContext.Update(gc);
                DbContext.SaveChanges();
                return CommandSuccess(
                    $"Greeting functionality on this guild has been **{mode.ToString().ToLower()}d**.");
            }

            [Command("greetings")]
            [Summary("Enable or disable greeting functionality for this guild.\n" +
                     "Supply no arguments to view the current status of it.\n" +
                     "Use `{prefix}greetchannel` to set the greeting channel.\n" +
                     "Use `{prefix}greetmessage` to set the greeting message.\n" +
                     "Use `{prefix}greettimeout` to set the greeting timeout.")]
            [Usage("greetings enable")]
            private Task<RuntimeResult> SetGreetingFunctionality()
                => CommandSuccess(
                    $"Greeting functionality on this guild is currently **{DbContext.GetOrCreateGuildConfig(Context.Guild).Greetings.ToString().ToLower()}d**.");

            [Command("greetchannel")]
            [Summary("Sets the guild's greeting channel.\n" +
                     "Supply no arguments to view the current channel (if set).\n" +
                     "Use `{prefix}greetings enable` to enable greetings.\n" +
                     "Use `{prefix}greetmessage` to change the greeting message.\n" +
                     "Use `{prefix}greettimeout` to change the greeting timeout.")]
            [Usage("greetchannel #welcome")]
            private Task<RuntimeResult> SetGreetingChannel(ITextChannel channel)
            {
				var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                gc.GreetChannelId = channel.Id;
                DbContext.Update(gc);
                DbContext.SaveChanges();
                return CommandSuccess($"Greeting channel for this guild has been updated to channel **{channel.Mention}** (`{channel.Id}`)");
            }
            
            [Command("greetchannel")]
            [Summary("Sets the guild's greeting channel.\n" +
                     "Supply no arguments to view the current channel (if set).\n" +
                     "Use `{prefix}greetings enable` to enable greetings.\n" +
                     "Use `{prefix}greetmessage` to change the greeting message.\n" +
                     "Use `{prefix}greettimeout` to change the greeting timeout.")]
            [Usage("greetchannel #welcome")]
            private Task<RuntimeResult> SetGreetingChannel()
            {
            	var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                
                if (Context.Guild.GetTextChannel(gc.GreetChannelId) is SocketTextChannel c)
                {
                	return CommandSuccess($"Greeting channel for this guild is currently channel {c.Mention} (`{c.Id}`).");
                }

                return CommandError("Greeting channel not found.", "Greeting channel for this guild is either not set or was deleted.");
            }

            [Command("greettimeout")]
            [Summary("Sets the guild's greeting message timeout.\n" +
                     "Supply no arguments to view the current timeout.\n" +
                     "To disable the timeout, simply supply `0`.\n" +
                     "Use `{prefix}greetings enable` to enable greetings.\n" +
                     "Use `{prefix}greetmessage` to change the greeting message.\n" +
                     "Use `{prefix}greetchannel` to set the greeting channel.\n")]
            [Usage("greettimeout 1m", "greettimeout 0")]
            private Task<RuntimeResult> SetGreetTimeout(TimeSpan timeout)
            {
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                gc.GreetTimeout = timeout;
                DbContext.Update(gc);
                DbContext.SaveChanges();
                return CommandSuccess(
                    $"Greeting timeout on this guild has been updated to **{StringExtensions.FormatTimeSpan(timeout)}**.");
            }

            [Command("greettimeout")]
            [Summary("Sets the guild's greeting message timeout.\n" +
                     "Supply no arguments to view the current timeout.\n" +
                     "To disable the timeout, simply supply `0`.\n" +
                     "Use `{prefix}greetings enable` to enable greetings.\n" +
                     "Use `{prefix}greetmessage` to change the greeting message.\n" +
                     "Use `{prefix}greetchannel` to set the greeting channel.\n")]
            [Usage("greettimeout 1m", "greettimeout 0")]
            private Task<RuntimeResult> SetGreetTimeout(int mustBeZero)
            {
                if (mustBeZero != 0) return CommandError("Input was non-zero.");
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                gc.GreetTimeout = null;
                DbContext.Update(gc);
                DbContext.SaveChanges();
                return CommandSuccess($"Greeting timeout on this guild has been **disabled**.");
            }

            [Command("greettimeout")]
            [Summary("Sets the guild's greeting message timeout.\n" +
                     "Supply no arguments to view the current timeout.\n" +
                     "To disable the timeout, simply supply `0`.\n" +
                     "Use `{prefix}greetings enable` to enable greetings.\n" +
                     "Use `{prefix}greetmessage` to change the greeting message.\n" +
                     "Use `{prefix}greetchannel` to set the greeting channel.\n")]
            [Usage("greettimeout 1m", "greettimeout 0")]
            private Task<RuntimeResult> SetGreetTimeout()
            {
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);

                if (gc.GreetTimeout is TimeSpan timeout)
                {
                    return CommandSuccess(
                        $"Greeting timeout on this guild is currently set to **{StringExtensions.FormatTimeSpan(timeout)}**.");
                }

                return CommandSuccess("Greeting timeout on this guild is currently **disabled**.");
            }

            [Command("greetmessage")]
            [Alias("greetmsg")]
            [Summary("Sets the guild's greeting message. Supports TOML embeds.\n" +
                     "Supply no arguments to view the currently set greeting message.\n" + 
                     "Use the placeholder `{guild.name}` and `{user.mention}` for the guild name and user mention, respectively.\n" +
                     "Use `{prefix}greetings enable` to enable greetings.\n" +
                     "Use `{prefix}greettimeout` to change the greeting timeout." +
                     "Use `{prefix}greetchannel` to set the greeting channel.\n")]
            [Usage("greetmessage Welcome to {guild}, {user}!")]
            private Task<RuntimeResult> SetGreetMessage([Remainder] string message)
            {
                if (string.IsNullOrWhiteSpace(message))
                    return CommandError("Message cannot be null or whitespace.",
                        "The greet message cannot be null or only whitespace.");

                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                gc.GreetMessage = message;
                DbContext.Update(gc);
                DbContext.SaveChanges();
                return CommandSuccess(
                    $"Greet message for this guild has been updated. Use `{DbContext.GetPrefixOrDefault(Context.Guild)}greetmessage` to view it.");
            }

            [Command("greetmessage")]
            [Alias("greetmsg")]
            [Summary("Sets the guild's greeting message. Supports TOML embeds.\n" +
                     "Supply no arguments to view the currently set greeting message.\n" + 
                     "Use the placeholder `{guild.name}` and `{user.mention}` for the guild name and user mention, respectively.\n" +
                     "Use `{prefix}greetings enable` to enable greetings.\n" +
                     "Use `{prefix}greettimeout` to change the greeting timeout." +
                     "Use `{prefix}greetchannel` to set the greeting channel.\n")]
            [Usage("greetmessage Welcome to {guild}, {user}!")]
            private Task<RuntimeResult> SetGreetMessage()
            {
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                var greetmsg = gc.GreetMessage.FormatPlaceHolders(Context.User, Context.Guild);
                return TomlEmbed.TryParse(greetmsg, out var result) ? CommandSuccess(result.Plaintext, result.ToEmbed()) : CommandSuccess(greetmsg);
            }

            [Command("upvotearrow")]
            [Summary("Sets the guild's upvote arrow emote.\n" +
                     "Supply no arguments to view the currently set emote.\n" +
                     "The `{prefix}vote` and `{prefix}suggest` commands utilize this emote.")]
            [Usage("upvotearrow ⬆")]
            private Task<RuntimeResult> SetUpvoteArrow(string emoteStr)
            {
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);

                if (Emote.TryParse(emoteStr, out var result))
                {
                    if (Context.Client.Guilds.SelectMany(x => x.Emotes).All(x => x.Id != result.Id))
                    {
                        return CommandError("No access to custom emote.", "I am not on the guild that emote is from and cannot use it.");
                    }

                    gc.UpvoteArrow = result.ToString();
                    DbContext.Update(gc);
                    DbContext.SaveChanges();
                    return CommandSuccess($"Upvote arrow emote on this guild has been updated to {result}.");
                }

                if (emoteStr.IsEmoji())
                {
                    gc.UpvoteArrow = emoteStr;
                    DbContext.Update(gc);
                    DbContext.SaveChanges();
                    return CommandSuccess($"Upvote arrow emote on this guild has been updated to {emoteStr}.");
                }

                return CommandError("Input was not valid Emoji.", "The input supplied was not valid Emoji.");
            }

            [Command("upvotearrow")]
            [Summary("Sets the guild's upvote arrow emote.\n" +
                     "Supply no arguments to view the currently set emote.\n" +
                     "The `{prefix}vote` and `{prefix}suggest` commands utilize this emote.")]
            [Usage("upvotearrow ⬆")]
            private Task<RuntimeResult> SetUpvoteArrow()
                => CommandSuccess(
                    $"Upvote arrow for this guild is currently set to {DbContext.GetOrCreateGuildConfig(Context.Guild).UpvoteArrow}");

            [Command("downvotearrow")]
            [Summary("Sets the guild's downvote arrow emote.\n" +
                     "Supply no arguments to view the currently set emote.\n" +
                     "The `{prefix}vote` and `{prefix}suggest` commands utilize this emote.")]
            [Usage("downvotearrow ⬆")]
            private Task<RuntimeResult> SetDownvoteArrow(string emoteStr)
            {
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);

                if (Emote.TryParse(emoteStr, out var result))
                {
                    if (Context.Client.Guilds.SelectMany(x => x.Emotes).All(x => x.Id != result.Id))
                    {
                        return CommandError("No access to custom emote.", "I am not on the guild that emote is from and cannot use it.");
                    }

                    gc.DownvoteArrow = result.ToString();
                    DbContext.Update(gc);
                    DbContext.SaveChanges();
                    return CommandSuccess($"Downvote arrow emote on this guild has been updated to {result}.");
                }

                if (emoteStr.IsEmoji())
                {
                    gc.DownvoteArrow = emoteStr;
                    DbContext.Update(gc);
                    DbContext.SaveChanges();
                    return CommandSuccess($"Downvote arrow emote on this guild has been updated to {emoteStr}.");
                }

                return CommandError("Input was not valid Emoji.", "The input supplied was not valid Emoji.");
            }

            [Command("downvotearrow")]
            [Summary("Sets the guild's downvote arrow emote.\n" +
                     "Supply no arguments to view the currently set emote.\n" +
                     "The `{prefix}vote` and `{prefix}suggest` commands utilize this emote.")]
            [Usage("upvotearrow ⬆")]
            private Task<RuntimeResult> SetDownvoteArrow()
                => CommandSuccess(
                    $"Downvote arrow for this guild is currently set to {DbContext.GetOrCreateGuildConfig(Context.Guild).DownvoteArrow}");

            [Command("trackrespects")]
            [Summary("Enables or disables respects-tracking functionality on this guild.\n" +
                     "Supply no arguments to view the current status of it.\n" +
                     "Typing \"F\" in any channel will pay your respects once per day.")]
            [Usage("trackrespects disable")]
            private Task<RuntimeResult> SetRespectsFunctionality(Functionality mode)
            {
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                gc.TrackRespects = mode;
                DbContext.Update(gc);
                DbContext.SaveChanges();
                return CommandSuccess(
                    $"Respects tracking functionality on this guild has been **{mode.ToString().ToLower()}d**.");
            }

            [Command("trackrespects")]
            [Summary("Enables or disables respects-tracking functionality on this guild.\n" +
                     "Supply no arguments to view the current status of it.\n" +
                     "Typing \"F\" in any channel will pay your respects once per day.")]
            [Usage("trackrespects disable")]
            private Task<RuntimeResult> SetRespectsFunctionality()
                => CommandSuccess(
                    $"Respects tracking functionality on this guild is currently **{DbContext.GetOrCreateGuildConfig(Context.Guild).TrackRespects.ToString().ToLower()}d**.");

            [Command("filterinvites")]
            [Summary("Enables or disables invite-filtering functionality on this guild.\n" +
                     "Any invites posted which are not from this guild will be automatically deleted.\n" +
                     "Supply no arguments to view the current status of it.\n")]
            [Usage("filterinvites enable")]
            [Remarks(
                "Note: in order to detect invites for this guild to ignore, the bot needs ManageGuild permissions.")]
            private Task<RuntimeResult> SetInviteFilteringFunctionality(Functionality mode)
            {
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                gc.FilterInvites = mode;
                DbContext.Update(gc);
                DbContext.SaveChanges();
                return CommandSuccess($"Invite filtering on this guild is now **{mode.ToString().ToLower()}d**.");
            }

            [Command("filterinvites")]
            [Summary("Enables or disables invite-filtering functionality on this guild.\n" +
                     "Any invites posted which are not from this guild will be automatically deleted.\n" +
                     "Supply no arguments to view the current status of it.\n")]
            [Usage("filterinvites enable")]
            [Remarks(
                "Note: in order to detect invites for this guild to ignore, the bot needs ManageGuild permissions.")]
            private Task<RuntimeResult> SetInviteFilteringFunctionality()
                => CommandSuccess(
                    $"Invite filtering on this guild is currently **{DbContext.GetOrCreateGuildConfig(Context.Guild).FilterInvites.ToString().ToLower()}d**.");

            [Command("invitecode")]
            [Summary("Sets the guild's custom invite code.\n" +
                     "Invite filtering will ignore this invite code.\n" +
                     "Use `{prefix} invite` to view the invite link.\n" +
                     "Supply no arguments to disable it.")]
            [Usage("invitecode aUvtGfj")]
            [Remarks("Note: only the code is needed, not the full link.")]
            private Task<RuntimeResult> SetInviteCode(string code = null)
            {
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                if (string.IsNullOrWhiteSpace(code))
                {
                    gc.InviteCode = null;
                    DbContext.Update(gc);
                    return CommandSuccess("Invite code for this guild has been **disabled**.");
                }

                gc.InviteCode = code;
                DbContext.Update(gc);
                DbContext.SaveChanges();
                return CommandSuccess($"Invite code for this guild has been updated to `{code}`.");
            }

            [Command("phraseminlength")]
            [Summary("Sets the guild's minimum phrase length.\n" +
                     "Supply no arguments to view the current minimum length.")]
            [Usage("phraseminlength 5")]
            [Remarks("Note: Length must be greater than or equal to {phraseminlength}.")]
            private Task<RuntimeResult> SetPhraseMinimumLength(ushort? length = null)
            {
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                if (length is null)
                {
                    return CommandSuccess(
                        $"Minimum phrase length for this guild is currently **{gc.MinimumPhraseLength}** characters.");
                }

                if (length < BotConfig.PHRASE_MIN_LENGTH)
                {
                    return CommandError(
                        $"Length shorter than global phrase length ({length} < {BotConfig.PHRASE_MIN_LENGTH}).",
                        $"Length must be greater than or equal to the global minimum (`{BotConfig.PHRASE_MIN_LENGTH}`).");
                }

                gc.MinimumPhraseLength = length.Value;
                DbContext.Update(gc);
                DbContext.SaveChanges();
                return CommandSuccess(
                    $"Minimum phrase length for this guild has been updated to **{length}** characters.");
            }

            [Command("verboseerrors")]
            [Summary("Enables or disables verbose errors for this guild.\n" +
                     "Nearly all command errors will result in a more verbose error message.\n" +
                     "Supply no arguments to view the current status of it.")]
            [Usage("verboseerrors enable")]
            private Task<RuntimeResult> SetVerboseErrors(Functionality mode)
            {
                var gc = DbContext.GetOrCreateGuildConfig(Context.Guild);
                gc.VerboseErrors = mode;
                DbContext.Update(gc);
                DbContext.SaveChanges();
                return CommandSuccess($"Verbose errors for this guild are now **{mode.ToString().ToLower()}d**.");
            }

            [Command("verboseerrors")]
            [Summary("Enables or disables verbose errors for this guild.\n" +
                     "Nearly all command errors will result in a more verbose error message.\n" +
                     "Supply no arguments to view the current status of it.")]
            [Usage("verboseerrors enable")]
            private Task<RuntimeResult> SetVerboseErrors()
                => CommandSuccess(
                    $"Verbose errors for this guild are currently **{DbContext.GetOrCreateGuildConfig(Context.Guild).VerboseErrors.ToString().ToLower()}d**.");
        }
    }
}