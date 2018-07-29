using System;
using System.Linq;
using System.Threading.Tasks;
using Administrator.Common;
using Administrator.Common.Attributes;
using Administrator.Common.Database;
using Administrator.Common.Database.Models;
using Administrator.Extensions;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using NLog.Fluent;

namespace Administrator.Modules.Moderation
{
    [Name("Moderation")]
    [RequirePermissionsPass]
    public class ModerationCommands : AdminBase
    {
        [Command("appeal")]
        [Summary("Appeal a ban or mute by ID with a supplied appeal message.")]
        [Usage("appeal 123 I apologize for my behavior.")]
        [Remarks("This command can only be used in bot DMs.")]
        [RequireContext(ContextType.DM)]
        private async Task<RuntimeResult> AppealAsync(uint id, [Remainder] string message)
        {
            if (!(Context.Database.Infractions.FirstOrDefault(x => x.ReceiverId == Context.User.Id && x.Id == id) is Infraction
                infraction))
                return await CommandError("No case found by that ID.", "No appeal-able case was found by that ID.");

            var gc = Context.Database.GetOrCreateGuildConfig(Context.Client.GetGuild(infraction.GuildId));
            switch (infraction)
            {
                case Ban ban:
                    if (ban.HasBeenAppealed)
                    {
                        return await CommandError("Ban has already been appealed.",
                            "You have already appealed your ban.");
                    }

                    if (string.IsNullOrWhiteSpace(message))
                    {
                        return await CommandError("No message specified.",
                            "You must provide a message in your appeal.");
                    }


                    ban.AppealMessage = message;
                    ban.AppealedTimestamp = DateTimeOffset.UtcNow;
                    Context.Database.Update(ban);
                    Context.Database.SaveChanges();

                    if (Context.Client.GetGuild(ban.GuildId)?
                            .GetTextChannel(gc.LogAppealChannelId) is SocketTextChannel c)
                    {
                        await c.EmbedAsync(new EmbedBuilder()
                            .WithOkColor()
                            .WithTitle($"Ban - Case #{ban.Id}")
                            .WithDescription($"**{ban.ReceiverName}** (`{ban.ReceiverId}`) has appealed their ban.\n```\n{ban.AppealMessage}\n```")
                            .WithFooter($"Use `{Context.Database.GetPrefixOrDefault(Context.Client.GetGuild(ban.GuildId))}revoke {ban.Id}` to revoke this ban.")
                            .WithTimestamp(ban.AppealedTimestamp)
                            .Build());
                    }

                    return await CommandSuccess("Your ban has been appealed and is awaiting review.");
                case Mute mute:
                    if (mute.HasBeenAppealed)
                    {
                        return await CommandError("Mute has already been appealed.",
                            "You have already appealed your mute.");
                    }

                    if (string.IsNullOrWhiteSpace(message))
                    {
                        return await CommandError("No message specified.",
                            "You must provide a message in your appeal.");
                    }

                    mute.AppealMessage = message;
                    mute.AppealedTimestamp = DateTimeOffset.UtcNow;
                    Context.Database.Update(mute);
                    Context.Database.SaveChanges();

                    if (Context.Client.GetGuild(mute.GuildId)?
                            .GetTextChannel(gc.LogAppealChannelId) is SocketTextChannel ch)
                    {
                        await ch.EmbedAsync(new EmbedBuilder()
                            .WithOkColor()
                            .WithTitle($"Mute - Case #{mute.Id}")
                            .WithDescription($"**{mute.ReceiverName}** (`{mute.ReceiverId}`) has appealed their mute.\n```\n{mute.AppealMessage}\n```")
                            .WithFooter($"Use `{Context.Database.GetPrefixOrDefault(Context.Client.GetGuild(mute.GuildId))}revoke {mute.Id}` to revoke this mute.")
                            .WithTimestamp(mute.AppealedTimestamp)
                            .Build());
                    }
                    return await CommandSuccess("Your mute has been appealed and is awaiting review.");
                default:
                    return await CommandError("Failed to match infraction to correct type.");
            }
        }

        [RequireContext(ContextType.Guild)]
        public class ModerationSubModule : ModerationCommands
        {
            [Command("ban")]
            [Alias("b")]
            [Summary("Ban a user with a supplied reason.")]
            [Usage("ban @SomeUser Your behavior is toxic.")]
            [RequireUserPermission(GuildPermission.BanMembers)]
            [RequireBotPermission(GuildPermission.BanMembers)]
            private async Task<RuntimeResult> BanUserAsync(SocketGuildUser receiver, [Remainder] string reason)
            {
                if (!(Context.User is SocketGuildUser issuer))
                    return await CommandError("Not a guild member.");

                if (issuer.Hierarchy <= receiver.Hierarchy)
                {
                    return await CommandError("User's hierarchy is lower than target's hierarchy.",
                        "You don't have permission to do that.");
                }

                if (Context.Guild.CurrentUser.Hierarchy <= receiver.Hierarchy)
                {
                    return await CommandError("Bot's hierarchy is lower than target's hierarchy.",
                        "I don't have permission to do that.");
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    return await CommandError("No reason supplied.", "You must supply a reason.");
                }

                var ban = Context.Database.Add(new Ban
                {
                    ReceiverId = receiver.Id,
                    ReceiverName = receiver.ToString(),
                    IssuerId = issuer.Id,
                    IssuerName = issuer.ToString(),
                    Reason = reason,
                    GuildId = Context.Guild.Id
                }).Entity;
                Context.Database.SaveChanges();

                await SendErrorAsync($"**{receiver}** has left the server [VAC banned from secure server]");

                try
                {
                    var dm = await receiver.GetOrCreateDMChannelAsync();
                    await dm.EmbedAsync(new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle($"You have been banned from {Context.Guild.Name}")
                        .AddField("Reason", reason)
                        .AddField("Appealing",
                            $"Your case ID is `{ban.Id}`. You may appeal this ban **__once__** and only **__once__**.\n" +
                            $"To appeal this ban, utilize the command `{BotConfig.Prefix}appeal {ban.Id} [your appeal here]`.\n\n" +
                            "If the bot does not accept your messages, join [ctf_turbine](https://discord.gg/ehpEkSP) to be able to share a guild with the bot,\n" +
                            "Or enable direct messages from server members in your client settings.")
                        .WithTimestamp(ban.Timestamp)
                        .Build());
                }
                catch
                {
                    // ignored
                }

                await Context.Guild.AddBanAsync(receiver, 7,
                    $"{reason} | Moderator: {issuer} | Issued: {ban.Timestamp:g} UTC | Case ID: {ban.Id}");

                return await CommandSuccess();
            }

            [Command("unban")]
            [Summary("Unban a user by user ID.")]
            [Usage("unban 0123456789")]
            [RequireUserPermission(GuildPermission.BanMembers)]
            [RequireBotPermission(GuildPermission.BanMembers)]
            private async Task<RuntimeResult> UnbanUserAsync(ulong receiverId)
            {
                var gc = Context.Database.GetOrCreateGuildConfig(Context.Guild);
                var bans = await Context.Guild.GetBansAsync();
                if (!(bans.FirstOrDefault(x => x.User.Id == receiverId) is RestBan guildBan))
                {
                    return await CommandError("User is not banned.",
                        "That user is not banned, or has been unbanned manually.");
                }

                var logChannel = Context.Guild.GetTextChannel(gc.LogUnbanChannelId) ?? Context.Channel;
                await Context.Guild.RemoveBanAsync(guildBan.User);
            
                if (Context.Database.Infractions.OfType<Ban>().Where(x => x.GuildId == gc.Id).OrderByDescending(x => x.Id)
                    .FirstOrDefault(x => x.ReceiverId == receiverId && !x.HasBeenRevoked) is Ban ban)
                {
                    ban.HasBeenRevoked = true;
                    ban.RevocationTimestamp = DateTimeOffset.UtcNow;
                    ban.RevokerId = Context.User.Id;
                    ban.RevokerName = Context.User.ToString();
                    Context.Database.Update(ban);
                    Context.Database.SaveChanges();
                
                    await logChannel.EmbedAsync(new EmbedBuilder()
                        .WithWarnColor()
                        .WithTitle($"Ban - Case #{ban.Id}")
                        .WithDescription($"User **{ban.ReceiverName}** has been unbanned.")
                        .WithModerator(Context.User)
                        .WithTimestamp(ban.RevocationTimestamp)
                        .Build());
                }
                else
                {
                    await logChannel.EmbedAsync(new EmbedBuilder()
                        .WithWarnColor()
                        .WithDescription($"User **{guildBan.User}** has been unbanned.")
                        .WithModerator(Context.User)
                        .Build());
                }

                return await CommandSuccess();
            }

            [Command("hackban")]
            [Summary("Ban a user by user ID, even if not on the guild, with a supplied reason.")]
            [Usage("hackban 0123456789 Circumventing punishment is not allowed.")]
            [Remarks("Note: this command will not generate a case or appeal.")]
            [RequireUserPermission(GuildPermission.BanMembers)]
            [RequireBotPermission(GuildPermission.BanMembers)]
            private async Task<RuntimeResult> HackbanUserAsync(ulong id, [Remainder] string reason)
            {
                if (!(Context.User is SocketGuildUser issuer)) return await CommandError("User is not guild member.");
                if (Context.Guild.GetUser(id) is SocketGuildUser receiver)
                {
                    if (issuer.Hierarchy <= receiver.Hierarchy)
                    {
                        return await CommandError("User's hierarchy is lower than target's hierarchy.",
                            "You don't have permission to do that.");
                    }

                    if (Context.Guild.CurrentUser.Hierarchy <= receiver.Hierarchy)
                    {
                        return await CommandError("Bot's hierarchy is lower than target's hierarchy.",
                            "I don't have permission to do that.");
                    }
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    return await CommandError("No reason supplied.", "You must supply a reason.");
                }

                var user = string.IsNullOrWhiteSpace(Context.Client.GetUser(id)?.ToString())
                    ? $"`{id}`"
                    : $"**{Context.Client.GetUser(id)}**";

                await SendErrorAsync($"{user} has left the server [Hackbanned from secure server]");
                await Context.Guild.AddBanAsync(id, 7, $"{reason} | Moderator: {issuer} | Issued: {DateTimeOffset.UtcNow:g} UTC");
                return await CommandSuccess();
            }

            [Command("revoke")]
            [Summary("Revoke a ban or mute by ID.")]
            [Usage("revoke 123")]
            [RequireBotPermission(GuildPermission.BanMembers | GuildPermission.ManageRoles)]
            [RequireUserPermission(GuildPermission.BanMembers | GuildPermission.MuteMembers)]
            private async Task<RuntimeResult> RevokeInfractionAsync(uint id)
            {
                var infractions = Context.Database.Infractions
                    .Where(x => x.GuildId == Context.Guild.Id)
                    .ToList();

                if (infractions.FirstOrDefault(x => x.Id == id) is Infraction infraction)
                {
                    var gc = Context.Database.GetOrCreateGuildConfig(Context.Client.GetGuild(infraction.GuildId));
                    switch (infraction)
                    {
                        case Ban ban:
                            if (ban.HasBeenRevoked)
                            {
                                return await CommandError("Ban already revoked.", "This ban has already been revoked.");
                            }

                            ban.HasBeenRevoked = true;
                            ban.RevocationTimestamp = DateTimeOffset.UtcNow;
                            ban.RevokerId = Context.User.Id;
                            ban.ReceiverName = Context.User.ToString();
                            Context.Database.Update(ban);
                            Context.Database.SaveChanges();
                            await Context.Guild.RemoveBanAsync(ban.ReceiverId);

                            if (Context.Guild.GetTextChannel(gc.LogBanChannelId) is null)
                            {
                                await EmbedAsync(new EmbedBuilder()
                                    .WithWarnColor()
                                    .WithTitle($"Ban - Case #{ban.Id}")
                                    .WithDescription(
                                        $"User **{ban.ReceiverName}** (`{ban.ReceiverId}`) has been unbanned.")
                                    .WithModerator(Context.User)
                                    .WithTimestamp(ban.RevocationTimestamp)
                                    .Build());
                            }
                            else
                            {
                                await SendOkAsync("Ban revoked.");
                            }

                            try
                            {
                                var dm = await Context.Client.GetUser(ban.ReceiverId).GetOrCreateDMChannelAsync();
                                await dm.EmbedAsync(new EmbedBuilder()
                                    .WithOkColor()
                                    .WithTitle($"Ban - Case #{ban.Id}")
                                    .WithDescription($"You have been unbanned from **{Context.Client.GetGuild(ban.GuildId).Name}**.")
                                    .WithTimestamp(ban.RevocationTimestamp)
                                    .Build());
                            }
                            catch
                            {
                                // ignored
                            }

                            return await CommandSuccess();
                        case Mute mute:
                            if (mute.HasBeenRevoked)
                            {
                                return await CommandError("Mute already revoked.", "This mute has already been revoked.");
                            }

                            if (Context.Guild.GetUser(mute.ReceiverId) is SocketGuildUser receiver
                                && Context.Guild.GetRole(gc.MuteRoleId) is SocketRole muteRole)
                            {
                                await receiver.RemoveRoleAsync(muteRole);
                                mute.HasBeenRevoked = true;
                                mute.RevocationTimestamp = DateTimeOffset.UtcNow;
                                mute.RevokerId = Context.User.Id;
                                mute.RevokerName = Context.User.ToString();
                                Context.Database.Update(mute);
                                Context.Database.SaveChanges();
                                await EmbedAsync(new EmbedBuilder()
                                    .WithOkColor()
                                    .WithTitle($"Mute - Case #{mute.Id}")
                                    .WithDescription(
                                        $"User **{mute.ReceiverName}** (`{mute.ReceiverId}`) has been unmuted.")
                                    .WithModerator(Context.User)
                                    .WithTimestamp(mute.RevocationTimestamp)
                                    .Build());
                                try
                                {
                                    var dm = await Context.Client.GetUser(mute.ReceiverId).GetOrCreateDMChannelAsync();
                                    await dm.EmbedAsync(new EmbedBuilder()
                                        .WithOkColor()
                                        .WithTitle($"Mute - Case #{mute.Id}")
                                        .WithDescription($"You have been unmuted in **{Context.Client.GetGuild(mute.GuildId).Name}**.")
                                        .WithTimestamp(mute.RevocationTimestamp)
                                        .Build());
                                }
                                catch
                                {
                                    // ignored
                                }

                                return await CommandSuccess();
                            }

                            return await CommandError("Could not unmute user.",
                                "Could not unmute that user. Verify that they are still in the guild and that the mute role is configured properly.");
                    }
                }

                return await CommandError("No ban or mute found by ID.", "No ban or mute found by that ID.");
            }

            [Command("softban")]
            [Alias("sb")]
            [Summary("Softban a user with a supplied reason.\nYou may supply the `-s` or `--silent` flag before the user to not send them a DM stating that they were softbanned.")]
            [Usage("sb @SomeUser Be a considerate human being.")]
            [RequireUserPermission(GuildPermission.KickMembers)]
            [RequireBotPermission(GuildPermission.BanMembers)]
            [Priority(1)]
            private async Task<RuntimeResult> SoftbanUserAsync(string flag, SocketGuildUser receiver, [Remainder] string reason)
            {
                if (!(Context.User is SocketGuildUser issuer)) return await CommandError("User is not guild member.");
                if (!flag.Equals("-s", StringComparison.OrdinalIgnoreCase)
                    && !flag.Equals("--silent", StringComparison.OrdinalIgnoreCase))
                {
                    if (Context.Guild.Users.FirstOrDefault(x =>
                        x.Username.Equals($"{flag} {receiver.Username}")
                        || x.Nickname?.Equals($"{flag} {receiver.Username}") == true) is SocketGuildUser newUser)
                    {
                        await SoftbanUserAsync(newUser, reason);
                    }
                }

                if (issuer.Hierarchy <= receiver.Hierarchy)
                {
                    return await CommandError("User's hierarchy is lower than target's hierarchy.",
                        "You don't have permission to do that.");
                }

                if (Context.Guild.CurrentUser.Hierarchy <= receiver.Hierarchy)
                {
                    return await CommandError("Bot's hierarchy is lower than target's hierarchy.",
                        "I don't have permission to do that.");
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    return await CommandError("No reason supplied.", "You must supply a reason.");
                }

                await SendErrorAsync($"**{receiver}** has left the server [Softbanned from secure server]");

                await Context.Guild.AddBanAsync(receiver, 7, $"{reason} | Moderator: {issuer} | Issued: {DateTimeOffset.UtcNow:g} UTC");
                await Context.Guild.RemoveBanAsync(receiver);
                return await CommandSuccess();
            }

            [Command("softban")]
            [Alias("sb")]
            [Summary("Softban a user with a supplied reason.\nYou may supply the `-s` or `--silent` flag before the user to not send them a DM stating that they were softbanned.")]
            [Usage("sb @SomeUser Be a considerate human being.")]
            [RequireUserPermission(GuildPermission.KickMembers)]
            [RequireBotPermission(GuildPermission.BanMembers)]
            [Priority(0)]
            private async Task<RuntimeResult> SoftbanUserAsync(SocketGuildUser receiver, [Remainder] string reason)
            {
                if (!(Context.User is SocketGuildUser issuer)) return await CommandError("User is not guild member.");

                if (issuer.Hierarchy <= receiver.Hierarchy)
                {
                    return await CommandError("User's hierarchy is lower than target's hierarchy.",
                        "You don't have permission to do that.");
                }

                if (Context.Guild.CurrentUser.Hierarchy <= receiver.Hierarchy)
                {
                    return await CommandError("Bot's hierarchy is lower than target's hierarchy.",
                        "I don't have permission to do that.");
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    return await CommandError("No reason supplied.", "You must supply a reason.");
                }

                await SendErrorAsync($"**{receiver}** has left the server [Softbanned from secure server]");

                try
                {
                    var dm = await receiver.GetOrCreateDMChannelAsync();
                    await dm.EmbedAsync(new EmbedBuilder()
                        .WithErrorColor()
                        .WithTitle($"You have been softbanned from {Context.Guild.Name}.")
                        .AddField("Reason", reason)
                        .WithTimestamp(DateTimeOffset.UtcNow)
                        .Build());
                }
                catch
                {
                    // ignored
                }

                await Context.Guild.AddBanAsync(receiver, 7, $"{reason} | Moderator: {issuer} | Issued: {DateTimeOffset.UtcNow:g} UTC");
                await Context.Guild.RemoveBanAsync(receiver);
                return await CommandSuccess();
            }
        
            [Command("kick")]
            [Alias("k")]
            [Summary("Kick a user with a supplied reason.\nYou may supply the `-s` or `--silent` flag before the user to not send them a DM stating that they were kicked.")]
            [Usage("kick @SomeUser Get out.")]
            [RequireUserPermission(GuildPermission.KickMembers)]
            [RequireBotPermission(GuildPermission.KickMembers)]
            [Priority(1)]
            private async Task<RuntimeResult> KickUserAsync(string flag, SocketGuildUser receiver, [Remainder] string reason)
            {
                if (!(Context.User is SocketGuildUser issuer)) return await CommandError("User is not guild member.");
                if (!flag.Equals("-s", StringComparison.OrdinalIgnoreCase)
                    && !flag.Equals("--silent", StringComparison.OrdinalIgnoreCase))
                {
                    if (Context.Guild.Users.FirstOrDefault(x =>
                        x.Username.Equals($"{flag} {receiver.Username}")
                        || x.Nickname?.Equals($"{flag} {receiver.Username}") == true) is SocketGuildUser newUser)
                    {
                        await KickUserAsync(newUser, reason);
                    }
                }

                if (issuer.Hierarchy <= receiver.Hierarchy)
                {
                    return await CommandError("User's hierarchy is lower than target's hierarchy.",
                        "You don't have permission to do that.");
                }

                if (Context.Guild.CurrentUser.Hierarchy <= receiver.Hierarchy)
                {
                    return await CommandError("Bot's hierarchy is lower than target's hierarchy.",
                        "I don't have permission to do that.");
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    return await CommandError("No reason supplied.", "You must supply a reason.");
                }

                await SendErrorAsync($"**{receiver}** has left the server [Kicked by moderator]");
                await receiver.KickAsync($"{reason} | Moderator: {issuer} | Issued: {DateTimeOffset.UtcNow:g} UTC");
                return await CommandSuccess();
            }

            [Command("kick")]
            [Alias("k")]
            [Summary("Kick a user with a supplied reason. You may supply the `-s` or `--silent` flag before the user to not send them a DM stating that they were kicked.")]
            [Usage("kick @SomeUser Get out.")]
            [RequireUserPermission(GuildPermission.KickMembers)]
            [RequireBotPermission(GuildPermission.KickMembers)]
            [Priority(0)]
            private async Task<RuntimeResult> KickUserAsync(SocketGuildUser receiver, [Remainder] string reason)
            {
                if (!(Context.User is SocketGuildUser issuer)) return await CommandError("User is not guild member.");

                if (issuer.Hierarchy <= receiver.Hierarchy)
                {
                    return await CommandError("User's hierarchy is lower than target's hierarchy.",
                        "You don't have permission to do that.");
                }

                if (Context.Guild.CurrentUser.Hierarchy <= receiver.Hierarchy)
                {
                    return await CommandError("Bot's hierarchy is lower than target's hierarchy.",
                        "I don't have permission to do that.");
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    return await CommandError("No reason supplied.", "You must supply a reason.");
                }

                await SendErrorAsync($"**{receiver}** has left the server [Kicked by moderator]");

                try
                {
                    var dm = await receiver.GetOrCreateDMChannelAsync();
                    await dm.EmbedAsync(new EmbedBuilder()
                        .WithErrorColor()
                        .WithTitle($"You have been kicked from {Context.Guild.Name}.")
                        .AddField("Reason", reason)
                        .WithTimestamp(DateTimeOffset.UtcNow)
                        .Build());
                }
                catch
                {
                    // ignored
                }

                await receiver.KickAsync($"{reason} | Moderator: {issuer} | Issued: {DateTimeOffset.UtcNow:g} UTC");
                return await CommandSuccess();
            }

            [Command("mute")]
            [Summary("Mute a user. You may specify just a reason (for a permanent mute), or a duration and reason.\n" +
                     "You may use `{prefix}mute @SomeUser #somechannel` to mute them from a guild channel or category.")]
            [Remarks("Mutes over 24 hours (including permanent mutes) may be appealed. Single channel mutes will not generate an appeal/case.")]
            [Usage("mute @SomeUser stop spamming.", "mute @SomeUser 1h Shut up.")]
            [RequireUserPermission(GuildPermission.MuteMembers)]
            [RequireBotPermission(GuildPermission.ManageRoles)]
            private async Task<RuntimeResult> MuteUserAsync(SocketGuildUser receiver, TimeSpan duration, [Remainder] string reason)
            {
                if (!(Context.User is SocketGuildUser issuer)) return await CommandError("User is not guild member.");

                if (issuer.Hierarchy <= receiver.Hierarchy)
                {
                    return await CommandError("User's hierarchy is lower than target's hierarchy.",
                        "You don't have permission to do that.");
                }

                if (Context.Guild.CurrentUser.Hierarchy <= receiver.Hierarchy)
                {
                    return await CommandError("Bot's hierarchy is lower than target's hierarchy.",
                        "I don't have permission to do that.");
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    return await CommandError("No reason supplied.", "You must supply a reason.");
                }

                var gc = Context.Database.GetOrCreateGuildConfig(Context.Guild);

                if (gc.MuteRole == Functionality.Disable)
                {
                    return await CommandError("Mute role disabled.", "This guild's mute role is currently disabled.");
                }

                if (!(Context.Guild.GetRole(gc.MuteRoleId) is SocketRole muteRole))
                {
                    return await CommandError("Invalid mute role ID.", "This guild's mute role is not set up or was deleted.");
                }

                if (Context.Database.Infractions.OfType<Mute>().Where(x => x.GuildId == gc.Id).Any(x => !x.HasBeenRevoked && !x.HasExpired && x.ReceiverId == receiver.Id)
                    || receiver.Roles.Any(x => x.Id == muteRole.Id))
                {
                    return await CommandError("Target already muted.", "That user is already muted.");
                }

                var ent = Context.Database.Add(new Mute
                {
                    ReceiverId = receiver.Id,
                    ReceiverName = receiver.ToString(),
                    IssuerId = issuer.Id,
                    IssuerName = issuer.ToString(),
                    Reason = reason,
                    GuildId = Context.Guild.Id,
                    Duration = (duration > TimeSpan.Zero) ? (TimeSpan?) duration : null
                });
                Context.Database.SaveChanges();
                var mute = Context.Database.Infractions.OfType<Mute>()
                    .First(x => x.Id == ent.Entity.Id);

                await SendWarnAsync(
                    $"**{receiver}** has been muted{(mute.Duration is TimeSpan ts ? $" until {DateTimeOffset.UtcNow.Add(ts):g} UTC ({StringExtensions.FormatTimeSpan(ts)})" : string.Empty)}.");

                var logChannel = Context.Guild.GetTextChannel(gc.LogMuteChannelId) ?? Context.Channel;
                await logChannel.EmbedAsync(new EmbedBuilder()
                    .WithWarnColor()
                    .WithTitle($"Mute - Case #{mute.Id}")
                    .WithDescription(
                        $"User **{receiver}** (`{receiver.Id}`) has been muted.")
                    .AddField("Reason", reason)
                    .AddField("Duration", mute.Duration is TimeSpan ts1 ? StringExtensions.FormatTimeSpan(ts1) : "Permanent")
                    .WithFooter($"Moderator: {issuer}", issuer.GetAvatarUrl())
                    .WithTimestamp(mute.Timestamp)
                    .Build());

                try
                {
                    var dm = await receiver.GetOrCreateDMChannelAsync();

                    await dm.EmbedAsync(new EmbedBuilder()
                        .WithWarnColor()
                        .WithTitle($"Mute - Case #{mute.Id}")
                        .WithDescription($"You have been muted in {Context.Guild.Name}")
                        .AddField("Reason", reason)
                        .AddField("Duration", mute.Duration is TimeSpan ts2 ? StringExtensions.FormatTimeSpan(ts2) : "Permanent")
                        .AddField("Appealing", !mute.CanBeAppealed  ? $"Your case ID is `{mute.Id}`.\nHowever, because of the short length of your mute, it cannot be appealed." :
                            $"Your case ID is `{mute.Id}`. You may appeal this mute **__once__** and only **__once__**.\n" +
                            $"To appeal this mute, utilize the command `{BotConfig.Prefix}appeal {mute.Id} [your appeal here]`.\n\n" +
                            "If the bot does not accept your messages, join [ctf_turbine](https://discord.gg/ehpEkSP) to be able to share a guild with the bot," +
                            "or enable direct messages from server members in your client settings.")
                        .WithTimestamp(mute.Timestamp)
                        .Build());
                }
                catch
                {
                    // ignored
                }

                await receiver.AddRoleAsync(muteRole);
                return await CommandSuccess();
            }

            [Command("mute")]
            [Summary("Mute a user. You may specify just a reason (for a permanent mute), or a duration and reason.\n" +
                     "You may use `{prefix}mute @SomeUser #somechannel` to mute them from a guild channel or category.")]
            [Remarks("Mutes over 24 hours (including permanent mutes) may be appealed. Single channel mutes will not generate an appeal/case.")]
            [Usage("mute @SomeUser stop spamming.", "mute @SomeUser 1h Shut up.")]
            [RequireUserPermission(GuildPermission.MuteMembers)]
            [RequireBotPermission(GuildPermission.ManageChannels)]
            [Priority(1)]
            private async Task<RuntimeResult> MuteUserAsync(SocketGuildUser receiver, IGuildChannel channel, [Remainder] string reason)
            {
                if ((Context.User as SocketGuildUser)?.Hierarchy <= receiver.Hierarchy)
                {
                    return await CommandError("User's hierarchy is lower than target's hierarchy.",
                        "You don't have permission to do that.");
                }

                if (Context.Guild.CurrentUser.Hierarchy <= receiver.Hierarchy)
                {
                    return await CommandError("Bot's hierarchy is lower than target's hierarchy.",
                        "I don't have permission to do that.");
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    return await CommandError("No reason supplied.", "You must supply a reason.");
                }

                switch (channel)
                {
                    case SocketVoiceChannel voiceChannel:
                        try
                        {
                            await voiceChannel.AddPermissionOverwriteAsync(receiver,
                                new OverwritePermissions(speak: PermValue.Deny, useVoiceActivation: PermValue.Deny));
                            return await CommandSuccess(
                                $"User **{receiver}** has been muted from voice channel **{voiceChannel.Name}**.");
                        }
                        catch
                        {
                            return await CommandError("Could not add text channel overwrite.",
                                "Could not add a permission overwrite for that user.\n" +
                                "Perhaps they are already muted?");
                        }
                    case SocketTextChannel textChannel:
                        try
                        {
                            await textChannel.AddPermissionOverwriteAsync(receiver,
                                new OverwritePermissions(sendMessages: PermValue.Deny, sendTTSMessages: PermValue.Deny,
                                    attachFiles: PermValue.Deny, addReactions: PermValue.Deny,
                                    useExternalEmojis: PermValue.Deny));
                            return await CommandSuccess(
                                $"User **{receiver}** has been muted from text channel {textChannel.Mention}.");
                        }
                        catch
                        {
                            return await CommandError("Could not add voice channel overwrite.",
                                "Could not add a permission overwrite for that user.\n" +
                                "Perhaps they are already muted?");
                        }
                    case SocketCategoryChannel categoryChannel:
                        var count = 0;
                        foreach (var chan in categoryChannel.Channels)
                        {
                            try
                            {
                                switch (chan)
                                {
                                    case SocketVoiceChannel categoryVoiceChannel:
                                        await categoryVoiceChannel.AddPermissionOverwriteAsync(receiver,
                                            new OverwritePermissions(speak: PermValue.Deny,
                                                useVoiceActivation: PermValue.Deny));
                                        break;
                                    case SocketTextChannel categoryTextChannel:
                                        await categoryTextChannel.AddPermissionOverwriteAsync(receiver,
                                            new OverwritePermissions(sendMessages: PermValue.Deny,
                                                sendTTSMessages: PermValue.Deny,
                                                attachFiles: PermValue.Deny, addReactions: PermValue.Deny,
                                                useExternalEmojis: PermValue.Deny));
                                        break;
                                }
                            }
                            catch
                            {
                                count++;
                            }
                        }

                        if (count > 0)
                        {
                            return await CommandSuccess(
                                $"User **{receiver}** has been muted from all channels of category **{categoryChannel.Name}**.\n" +
                                $"Note: {count} channel(s) already had a permission overwrite for this user.");
                        }

                        return await CommandSuccess(
                            $"User **{receiver}** has been muted from all channels of category **{categoryChannel.Name}**.");
                    default:
                        Console.WriteLine(channel.GetType());
                        return await CommandError("Incorrect channel type.",
                            "The supplied channel type must be either a category, text, or voice channel.");
                }
            }

            [Command("mute")]
            [Summary("Mute a user. You may specify just a reason (for a permanent mute), or a duration and reason.\n" +
                     "You may use `{prefix}mute @SomeUser #somechannel` to mute them from a guild channel or category.")]
            [Remarks("Mutes over 24 hours (including permanent mutes) may be appealed. Single channel mutes will not generate an appeal/case.")]
            [Usage("mute @SomeUser stop spamming.", "mute @SomeUser 1h Shut up.")][RequireUserPermission(GuildPermission.MuteMembers)]
            [RequireBotPermission(GuildPermission.ManageRoles)]
            [Priority(0)]
            private async Task<RuntimeResult> MuteUserAsync(SocketGuildUser receiver, [Remainder] string reason)
                => await MuteUserAsync(receiver, TimeSpan.Zero, reason);

            [Command("unmute")]
            [Summary("Unmutes a muted user.\n" +
                     "You may use `{prefix}unmute @SomeUser #somechannel` to unmute them from a guild channel or category (if they are already muted).")]
            [Usage("unmute @SomeUser")]
            [RequireUserPermission(GuildPermission.MuteMembers)]
            [RequireBotPermission(GuildPermission.ManageRoles)]
            private async Task<RuntimeResult> UnmuteUserAsync(SocketGuildUser receiver)
            {
                var gc = Context.Database.GetOrCreateGuildConfig(Context.Guild);
                if (!(Context.Guild.GetRole(gc.MuteRoleId) is SocketRole muteRole))
                {
                    return await CommandError("Invalid mute role ID.", "This guild's mute role is not set up or was deleted.");
                }

                if (receiver.Roles.All(x => x.Id != muteRole.Id))
                {
                    return await CommandError("Target is not muted.",
                        "That user is not muted, or had the mute role removed manually.");
                }

                await receiver.RemoveRoleAsync(muteRole);
                var logChannel = Context.Guild.GetTextChannel(gc.LogMuteChannelId) ?? Context.Channel;
            
                if (Context.Database.Infractions.OfType<Mute>().Where(x => x.GuildId == gc.Id).OrderByDescending(x => x.Id)
                    .FirstOrDefault(x => x.ReceiverId == receiver.Id && !x.HasBeenRevoked) is Mute mute)
                {
                    mute.HasBeenRevoked = true;
                    mute.RevocationTimestamp = DateTimeOffset.UtcNow;
                    mute.RevokerId = Context.User.Id;
                    mute.RevokerName = Context.User.ToString();
                    Context.Database.Update(mute);
                    Context.Database.SaveChanges();

                    await logChannel.EmbedAsync(new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle($"Mute - Case #{mute.Id}")
                        .WithDescription($"User **{receiver}** has been unmuted.")
                        .WithModerator(Context.User)
                        .WithTimestamp(mute.RevocationTimestamp)
                        .Build());
                }
                else
                {
                    await logChannel.EmbedAsync(new EmbedBuilder()
                        .WithOkColor()
                        .WithDescription($"User **{receiver}** has been unmuted.")
                        .WithModerator(Context.User)
                        .Build());
                }

                return await CommandSuccess();
            }

            [Command("unmute")]
            [Summary("Unmutes a muted user.\n" +
                     "You may use `{prefix}unmute @SomeUser #somechannel` to unmute them from a guild channel or category (if they are already muted).")]
            [Usage("unmute @SomeUser")]
            [RequireUserPermission(GuildPermission.MuteMembers)]
            [RequireBotPermission(GuildPermission.ManageRoles)]
            private async Task<RuntimeResult> UnmuteUserAsync(SocketGuildUser receiver, IGuildChannel channel)
            {
                if ((Context.User as SocketGuildUser)?.Hierarchy <= receiver.Hierarchy)
                {
                    return await CommandError("User's hierarchy is lower than target's hierarchy.",
                        "You don't have permission to do that.");
                }

                if (Context.Guild.CurrentUser.Hierarchy <= receiver.Hierarchy)
                {
                    return await CommandError("Bot's hierarchy is lower than target's hierarchy.",
                        "I don't have permission to do that.");
                }

                switch (channel)
                {
                    case SocketVoiceChannel voiceChannel:
                        try
                        {
                            await voiceChannel.RemovePermissionOverwriteAsync(receiver);
                            return await CommandSuccess(
                                $"User **{receiver}** has been unmuted from voice channel **{voiceChannel.Name}**.");
                        }
                        catch
                        {
                            return await CommandError("Could not remove text channel overwrite.",
                                "Could not remove a permission overwrite for that user.\n" +
                                "Perhaps they are already unmuted?");
                        }
                    case SocketTextChannel textChannel:
                        try
                        {
                            await textChannel.RemovePermissionOverwriteAsync(receiver);
                            return await CommandSuccess(
                                $"User **{receiver}** has been unmuted from text channel {textChannel.Mention}.");
                        }
                        catch
                        {
                            return await CommandError("Could not remove voice channel overwrite.",
                                "Could not remove a permission overwrite for that user.\n" +
                                "Perhaps they are already unmuted?");
                        }
                    case SocketCategoryChannel categoryChannel:
                        var count = 0;
                        foreach (var chan in categoryChannel.Channels)
                        {
                            try
                            {
                                switch (chan)
                                {
                                    case SocketVoiceChannel categoryVoiceChannel:
                                        await categoryVoiceChannel.RemovePermissionOverwriteAsync(receiver);
                                        break;
                                    case SocketTextChannel categoryTextChannel:
                                        await categoryTextChannel.RemovePermissionOverwriteAsync(receiver);
                                        break;
                                }
                            }
                            catch
                            {
                                count++;
                            }
                        }

                        if (count > 0)
                        {
                            return await CommandSuccess(
                                $"User **{receiver}** has been unmuted from all channels of category **{categoryChannel.Name}**.\n" +
                                $"Note: {count} channel(s) already had no permission overwrites for this user.");
                        }

                        return await CommandSuccess(
                            $"User **{receiver}** has been unmuted from all channels of category **{categoryChannel.Name}**.");
                    default:
                        return await CommandError("Incorrect channel type.",
                            "The supplied channel type must be either a category, text, or voice channel.");
                }
            }


            [Command("warn")]
            [Summary("Warns a user with a specified reason.\n" +
                     "If a user accumulates warnings, they will automatically be punished.\n" +
                     "See the command `{prefix}warnpunish` for more information.")]
            [Usage("warn @SomeUser don't spam.")]
            private async Task<RuntimeResult> WarnUserAsync(SocketGuildUser receiver, [Remainder] string reason)
            {
                if (!(Context.User is SocketGuildUser issuer)) return await CommandError("User is not guild member.");

                if (issuer.Hierarchy <= receiver.Hierarchy)
                {
                    return await CommandError("User's hierarchy is lower than target's hierarchy.",
                        "You don't have permission to do that.");
                }

                if (Context.Guild.CurrentUser.Hierarchy <= receiver.Hierarchy)
                {
                    return await CommandError("Bot's hierarchy is lower than target's hierarchy.",
                        "I don't have permission to do that.");
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    return await CommandError("No reason supplied.", "You must supply a reason.");
                }

                var warning = Context.Database.Add(new Warning
                {
                    GuildId = Context.Guild.Id,
                    IssuerId = issuer.Id,
                    ReceiverId = receiver.Id
                }).Entity;
                Context.Database.SaveChanges();

                var gc = Context.Database.GetOrCreateGuildConfig(Context.Guild);

                var logChannel = Context.Guild.GetTextChannel(gc.LogWarnChannelId) ?? Context.Channel;

                var warnings =
                    Context.Database.Warnings.Where(x => x.ReceiverId == receiver.Id && x.GuildId == Context.Guild.Id)
                        .ToList();
                var warningPunishments =
                    Context.Database.WarningPunishments.Where(x => x.GuildId == Context.Guild.Id).ToList();
                if (warningPunishments.FirstOrDefault(x => x.Count == warnings.Count) is WarningPunishment wp)
                {
                    switch (wp.Type)
                    {
                        case PunishmentType.Mute:
                            if (wp.MuteDuration.HasValue)
                            {
                                await MuteUserAsync(receiver, wp.MuteDuration.Value,
                                    $"**{wp.Count}** warnings reached - automatic mute - see warning case #{warning.Id}.\n\"{reason}\"");
                            }
                            else
                            {
                                await MuteUserAsync(receiver,
                                    $"**{wp.Count}** warnings reached - automatic mute - see warning case #{warning.Id}.\n\"{reason}\"");
                            }
                            break;
                        case PunishmentType.Kick:
                            await KickUserAsync(receiver,
                                $"**{wp.Count}** warnings reached - automatic kick - see warning case #{warning.Id}.\n\"{reason}\"");
                            break;
                        case PunishmentType.Softban:
                            await SoftbanUserAsync(receiver,
                                $"**{wp.Count}** warnings reached - automatic softban - see warning case #{warning.Id}.\n\"{reason}\"");
                            break;
                        case PunishmentType.Ban:
                            await BanUserAsync(receiver,
                                $"**{wp.Count}** warnings reached - automatic ban - see warning case #{warning.Id}.\n\"{reason}\"");
                            break;
                    }

                    await logChannel.EmbedAsync(new EmbedBuilder()
                        .WithWarnColor()
                        .WithTitle($"Warn - Case #{warning.Id}")
                        .WithDescription(
                            $"User **{receiver}** (`{receiver.Id}`) has been warned and punishment {wp.Type}{(wp.Type == PunishmentType.Mute && wp.MuteDuration.HasValue ? $" ({StringExtensions.FormatTimeSpan(wp.MuteDuration.Value)})" : string.Empty)} has been applied.")
                        .AddField("Reason", warning.Reason)
                        .WithModerator(Context.User)
                        .WithTimestamp(warning.Timestamp)
                        .Build());

                    if (logChannel.Id != Context.Channel.Id)
                    {
                        await SendWarnAsync($"**{receiver}** has been warned.");
                    }

                    return await CommandSuccess();
                }

                await logChannel.EmbedAsync(new EmbedBuilder()
                    .WithWarnColor()
                    .WithTitle($"Warn - Case #{warning.Id}")
                    .WithDescription(
                        $"User **{receiver}** (`{receiver.Id}`) has been warned.")
                    .AddField("Reason", warning.Reason)
                    .WithModerator(Context.User)
                    .WithTimestamp(warning.Timestamp)
                    .Build());

                if (logChannel.Id != Context.Channel.Id)
                {
                    await SendWarnAsync($"**{receiver}** has been warned.");
                }

                return await CommandSuccess();
            }

            [Command("warnpunishlist")]
            [Alias("warnpl")]
            [Summary("View the current warning punishment list.")]
            [Usage("warnpl")]
            private Task<RuntimeResult> GetWarningPunishments()
            {
                if (!Context.Database.WarningPunishments.Any(x => x.GuildId == Context.Guild.Id))
                    return CommandError("No punishments found.",
                        "No punishments found on this guild.");

                return CommandSuccess(embed: new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle($"Warning punishments for {Context.Guild.Name}")
                    .WithDescription(string.Join("\n", Context.Database.WarningPunishments
                        .Where(x => x.GuildId == Context.Guild.Id)
                        .OrderBy(x => x.Count)
                        .Select(x =>
                            $"{x.Count} => {x.Type} {(x.Type == PunishmentType.Mute ? $"{(x.MuteDuration.HasValue ? $"({StringExtensions.FormatTimeSpan(x.MuteDuration.Value)})" : "(permanent)")}" : string.Empty)}")))
                    .Build());
            }

            [Command("warnpunish")]
            [Alias("warnp")]
            [Summary("Set a particular punishment type for a certain number of warnings.\n" +
                     "Supported types are: `Mute`, `Kick`, `Softban`, `Ban`\n" +
                     "You may specify no type to remove the warning punishment for that warning count.")]
            [Usage("warnp 2 mute 1h", "warnp 3 kick", "warnp 5")]
            [Remarks(
                "When specifying a mute, you may specify a duration for the mute, or it will default to being permanent.")]
            [RequirePermRole]
            [RequireUserPermission(GuildPermission.BanMembers)]
            private Task<RuntimeResult> SetWarningPunishment(uint count)
            {
                if (count == 0)
                    return CommandError("Warning count must be greater than zero.",
                        "You cannot apply a punishment for 0 warnings.");

                if (!(Context.Database.WarningPunishments
                        .FirstOrDefault(x => x.Count == count && x.GuildId == Context.Guild.Id)
                    is WarningPunishment wp))
                {
                    return CommandError("No punishment found with that count.",
                        "No punishments exist for that number of warnings.");
                }

                Context.Database.Remove(wp);
                Context.Database.SaveChanges();
                return CommandSuccess(
                    $"I will no longer apply a punishment to users who reach **{count}** warning(s).");
            }

            [Command("warnpunish")]
            [Alias("warnp")]
            [Summary("Set a particular punishment type for a certain number of warnings.\n" +
                     "Supported types are: `Mute`, `Kick`, `Softban`, `Ban`\n" +
                     "You may specify no type to remove the warning punishment for that warning count.")]
            [Usage("warnp 2 mute 1h", "warnp 3 kick", "warnp 5")]
            [Remarks(
                "When specifying a mute, you may specify a duration for the mute, or it will default to being permanent.")]
            [RequirePermRole]
            [RequireUserPermission(GuildPermission.BanMembers)]
            private Task<RuntimeResult> SetWarningPunishment(uint count, PunishmentType type)
            {
                if (count == 0)
                    return CommandError("Warning count must be greater than zero.",
                        "You cannot apply a punishment for 0 warnings.");

                if (Context.Database.WarningPunishments.FirstOrDefault(x => x.Count == count && x.GuildId == Context.Guild.Id)
                    is WarningPunishment wp)
                {
                    wp.Type = type;
                    wp.MuteDuration = null;
                    Context.Database.Update(wp);
                }
                else
                {
                    Context.Database.Add(new WarningPunishment
                    {
                        GuildId = Context.Guild.Id,
                        Count = count,
                        Type = type
                    });
                }

                Context.Database.SaveChanges();
                return CommandSuccess(
                    $"I will now **{type.ToString().ToLower()}** users who reach **{count}** warning(s).");
            }

            [Command("warnpunish")]
            [Alias("warnp")]
            [Summary("Set a particular punishment type for a certain number of warnings.\n" +
                     "Supported types are: `Mute`, `Kick`, `Softban`, `Ban`\n" +
                     "You may specify no type to remove the warning punishment for that warning count.")]
            [Usage("warnp 2 mute 1h", "warnp 3 kick", "warnp 5")]
            [Remarks(
                "When specifying a mute, you may specify a duration for the mute, or it will default to being permanent.")]
            [RequirePermRole]
            [RequireUserPermission(GuildPermission.BanMembers)]
            private Task<RuntimeResult> SetWarningPunishment(uint count, PunishmentType type, TimeSpan muteDuration)
            {
                if (type == PunishmentType.Mute)
                    return CommandError("Cannot set a duration for punishments other than mute.",
                        "Punishments other than type **Mute** cannot have a duration associated with them.");
                if (muteDuration < TimeSpan.FromMinutes(5))
                    return CommandError("Mute duration is less than 5 minutes.",
                        "The mute duration has a minimum of 5 minutes.");
                if (count == 0)
                    return CommandError("Warning count must be greater than zero.",
                        "You cannot apply a punishment for 0 warnings.");

                if (Context.Database.WarningPunishments.FirstOrDefault(x => x.Count == count && x.GuildId == Context.Guild.Id)
                    is WarningPunishment wp)
                {
                    wp.Type = type;
                    wp.MuteDuration = muteDuration;
                    Context.Database.Update(wp);
                }
                else
                {
                    Context.Database.Add(new WarningPunishment
                    {
                        GuildId = Context.Guild.Id,
                        Count = count,
                        MuteDuration = muteDuration,
                        Type = type
                    });
                }

                Context.Database.SaveChanges();
                return CommandSuccess(
                    $"I will now mute users for **{StringExtensions.FormatTimeSpan(muteDuration)}** upon reaching **{count}** warning(s).");
            }
        }
    }
}
