using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    public class GuildConfigCommands : ModuleBase<SocketCommandContext>
    {
        private static readonly Config Config = BotConfig.New();
        private readonly DbService _db;

        public GuildConfigCommands(DbService db)
        {
            _db = db;
        }

        [Command("permrole")]
        [Alias("pr")]
        [Usage("{p}permrole Admin")]
        [Summary("Gets or sets permission role for this guild.")]
        [RequireContext(ContextType.Guild)]
        //[RequireUserPermission(GuildPermission.Administrator)]
        private async Task GetSetPermRoleAsync([Remainder] string roleName = null)
        {
            var eb = new EmbedBuilder();
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);
            if (!(Context.User is SocketGuildUser user)) return;
            if (string.IsNullOrWhiteSpace(roleName)
                && Context.Guild.Roles.FirstOrDefault(x => x.Id == (ulong) gc.PermRole) is SocketRole r)
            {
                eb.WithOkColor()
                    .WithDescription($"Permrole on this server is {r.Name}");
            }
            else if (!user.GuildPermissions.Administrator)
            {
                eb.WithErrorColor()
                    .WithDescription(
                        "You don't have permission to do that. Contact a user with Administrator guild permissions.");
            }
            else if (Context.Guild.Roles.OrderByDescending(x => x.Position)
                    .FirstOrDefault(x => x.Name.Equals(roleName, StringComparison.InvariantCultureIgnoreCase))
                is SocketRole newPermRole)
            {
                gc.PermRole = (long) newPermRole.Id;
                await _db.UpdateAsync(gc).ConfigureAwait(false);
                eb.WithOkColor()
                    .WithDescription($"Guild permission role has been updated to role **{newPermRole.Name}**.");
            }
            else
            {
                eb.WithErrorColor()
                    .WithDescription("Could not find a role matching that name.");
            }

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        [Command("showguildconfig")]
        [Alias("showgc", "sgc")]
        [Summary("Displays bot configuration for this guild.")]
        [Usage("{p}showguildconfig")]
        [RequirePermRole]
        [RequireContext(ContextType.Guild)]
        private async Task ShowGuildConfigAsync()
        {
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            var eb = new EmbedBuilder()
                .WithOkColor()
                /*
                .WithAuthor(new EmbedAuthorBuilder
                {
                    IconUrl = Context.Guild.IconUrl,
                    Name = $"Bot configuration for {Context.Guild.Name}"
                })
                */
                .WithTitle($"Bot configuration for {Context.Guild.Name}")
                .WithThumbnailUrl(Context.Guild.IconUrl)
                .WithDescription(
                    $"**LogChannel**: {(gc.LogChannel == default ? "Not set" : Context.Guild.TextChannels.FirstOrDefault(x => x.Id == (ulong) gc.LogChannel)?.Mention)}\n" +
                    $"**SuggestionChannel**: {(gc.SuggestionChannel == default ? "Not set" : Context.Guild.TextChannels.FirstOrDefault(x => x.Id == (ulong) gc.SuggestionChannel)?.Mention)}\n" +
                    $"**SuggestionArchive**: {(gc.SuggestionArchive == default ? "Not set" : Context.Guild.TextChannels.FirstOrDefault(x => x.Id == (ulong) gc.SuggestionArchive)?.Mention)}\n" +
                    $"**GreetChannel**: {(gc.GreetChannel == default ? "Not set" : Context.Guild.TextChannels.FirstOrDefault(x => x.Id == (ulong) gc.GreetChannel)?.Mention)}\n\n" +
                    $"**PermRole**: {(Context.Guild.Roles.FirstOrDefault(x => x.Id == (ulong) gc.PermRole) is SocketRole permRole ? permRole.Name : "Not set")}\n" +
                    $"**MuteRole**: {(Context.Guild.Roles.FirstOrDefault(x => x.Id == (ulong) gc.MuteRole) is SocketRole muteRole ? muteRole.Name : "Not set")}\n" +
                    $"**LookingToPlayRole**: {(Context.Guild.Roles.FirstOrDefault(x => x.Id == (ulong) gc.LookingToPlayRole) is SocketRole ltpRole ? ltpRole.Name : "Not set")}\n" +
                    $"**LookingToPlayMaxHours**: {(gc.LookingToPlayMaxHours == default ? "None" : $"{gc.LookingToPlayMaxHours} hours")}\n\n" +
                    $"**UpvoteArrow**: {gc.UpvoteArrow}\n" +
                    $"**DownvoteArrow**: {gc.DownvoteArrow}\n\n" +
                    $"**VerboseErrors**: {gc.VerboseErrors}\n" +
                    $"**GreetUserOnJoin**: {gc.GreetUserOnJoin}\n" +
                    $"**MentionUserOnJoin**: {gc.MentionUserOnJoin}\n" + 
                    $"**GreetMessage**: \"{gc.GreetMessage}\"\n" +
                    $"**GreetTimeout**: {(gc.GreetTimeout == default ? "None" : $"{gc.GreetTimeout} seconds")}\n" + 
                    $"**EnableRespects**: {gc.EnableRespects}\n" +
                    $"**InviteFiltering**: {gc.InviteFiltering}\n" +
                    $"**PhraseMinLength**: {gc.PhraseMinLength} characters");

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        [Command("modifyguildconfig")]
        [Alias("modifygc", "mgc")]
        [Summary("Modify your guild's bot configuration.")]
        [Usage("{p}mgc MuteRole Silenced", "{p}mgc LogChannel #logs")]
        [RequirePermRole]
        [RequireContext(ContextType.Guild)]
        private async Task ModifyGuildConfigAsync(string property, [Remainder] string value)
        {
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);
            var eb = new EmbedBuilder();
            var prop = string.Empty;
            var val = string.Empty;

            switch (property.ToLower())
            {
                case "logchannel":
                    if (Context.Guild.TryParseTextChannel(value, out var logChannel))
                    {
                        gc.LogChannel = (long) logChannel.Id;
                        prop = "LogChannel";
                        val = logChannel.Mention;
                    }
                    else
                    {
                        await Context.Channel.SendErrorAsync("Could not find a valid text channel for that input.")
                            .ConfigureAwait(false);
                    } 

                    break;
                case "suggestionchannel":
                    if (Context.Guild.TryParseTextChannel(value, out var suggestionChannel))
                    {
                        gc.SuggestionChannel = (long) suggestionChannel.Id;
                        prop = "SuggestionChannel";
                        val = suggestionChannel.Mention;
                    }
                    else
                    {
                        await Context.Channel.SendErrorAsync("Could not find a valid text channel for that input.")
                            .ConfigureAwait(false);
                    }

                    break;
                case "muterole":
                    if (Context.Guild.Roles.FirstOrDefault(x =>
                        x.Name.Equals(value, StringComparison.InvariantCultureIgnoreCase)) is SocketRole muteRole)
                    {
                        gc.MuteRole = (long) muteRole.Id;
                        prop = "MuteRole";
                        val = muteRole.Name;
                        var textperms = new OverwritePermissions(sendMessages: PermValue.Deny, addReactions: PermValue.Deny);
                        var voiceperms = new OverwritePermissions(speak: PermValue.Deny);

                        Parallel.ForEach(Context.Guild.TextChannels,
                            async channel =>
                                await channel.AddPermissionOverwriteAsync(muteRole, textperms).ConfigureAwait(false));

                        Parallel.ForEach(Context.Guild.VoiceChannels,
                            async channel =>
                                await channel.AddPermissionOverwriteAsync(muteRole, voiceperms).ConfigureAwait(false));
                    }
                    else
                    {
                        await Context.Channel.SendErrorAsync("Could not find a role name matching that input.")
                            .ConfigureAwait(false);
                    }

                    break;
                case "lookingtoplayrole":
                    if (Context.Guild.Roles.FirstOrDefault(x =>
                        x.Name.Equals(value, StringComparison.InvariantCultureIgnoreCase)) is SocketRole ltpRole)
                    {
                        gc.LookingToPlayRole = (long) ltpRole.Id;
                        prop = "LookingToPlayRole";
                        val = ltpRole.Name;
                    }
                    else
                    {
                        await Context.Channel.SendErrorAsync("Could not find a role name matching that input.")
                            .ConfigureAwait(false);
                    }

                    break;
                case "lookingtoplaymaxhours":
                    if (ulong.TryParse(value, out var ltpTimeout))
                    {
                        gc.LookingToPlayMaxHours = (long) ltpTimeout;
                        prop = "LookingToPlayMaxHours";
                        val = ltpTimeout.ToString();
                    }
                    else
                    {
                        await Context.Channel
                            .SendErrorAsync("Value must be a non-negative integer.")
                            .ConfigureAwait(false);
                    }

                    break;
                case "mentionltpusers":
                    if (bool.TryParse(value, out var mentionLtpUsers))
                    {
                        gc.MentionLtpUsers = mentionLtpUsers;
                        prop = "MentionLtpUsers";
                        val = mentionLtpUsers.ToString();
                        if (Context.Guild.Roles.FirstOrDefault(x => x.Id == (ulong) gc.LookingToPlayRole) is SocketRole ltp
                            && !ltp.IsMentionable)
                        {
                            await ltp.ModifyAsync(x => x.Mentionable = true).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        await Context.Channel.SendErrorAsync("Value must be a boolean (`true` or `false`)")
                            .ConfigureAwait(false);
                    }

                    break;
                case "suggestionarchive":
                    if (Context.Guild.TryParseTextChannel(value, out var suggestionArchive))
                    {
                        gc.SuggestionArchive = (long) suggestionArchive.Id;
                        prop = "SuggestionArchive";
                        val = suggestionArchive.Mention;
                    }
                    else
                    {
                        await Context.Channel.SendErrorAsync("Could not find a valid text channel for that input.")
                            .ConfigureAwait(false);
                    }

                    break;
                case "greetchannel":
                    if (Context.Guild.TryParseTextChannel(value, out var greetChannel))
                    {
                        gc.GreetChannel = (long) greetChannel.Id;
                        prop = "GreetChannel";
                        val = greetChannel.Mention;
                    }
                    else
                    {
                        await Context.Channel.SendErrorAsync("Could not find a valid text channel for that input.")
                            .ConfigureAwait(false);
                    }

                    break;
                case "upvotearrow":
                    gc.UpvoteArrow = value;
                    prop = "UpvoteArrow";;
                    val = value +
                          "\n(note, if this doesn't look correct, the bot may not be on the guild this emote is on.)";
                    break;
                case "downvotearrow":
                    gc.DownvoteArrow = value;
                    prop = "DownvoteArrow";;
                    val = value +
                          "\n(note, if this doesn't look correct, the bot may not be on the guild this emote is on.)";
                    break;
                case "verboseerrors":
                    if (bool.TryParse(value, out var verboseErrors))
                    {
                        gc.VerboseErrors = verboseErrors;
                        prop = "VerboseErrors";
                        val = verboseErrors.ToString();
                    }
                    else
                    {
                        await Context.Channel.SendErrorAsync("Value must be a boolean (`true` or `false`)")
                            .ConfigureAwait(false);
                    }

                    break;
                case "greetuseronjoin":
                    if (bool.TryParse(value, out var greetUserOnJoin))
                    {
                        gc.GreetUserOnJoin = greetUserOnJoin;
                        prop = "GreetUserOnJoin";
                        val = greetUserOnJoin.ToString();
                    }
                    else
                    {
                        await Context.Channel.SendErrorAsync("Value must be a boolean (`true` or `false`)")
                            .ConfigureAwait(false);
                    }

                    break;
                case "mentionuseronjoin":
                    if (bool.TryParse(value, out var mentionUserOnJoin))
                    {
                        gc.MentionUserOnJoin = mentionUserOnJoin;
                        prop = "MentionUserOnJoin";
                        val = mentionUserOnJoin.ToString();
                    }
                    else
                    {
                        await Context.Channel.SendErrorAsync("Value must be a boolean (`true` or `false`)")
                            .ConfigureAwait(false);
                    }

                    break;
                case "greetmessage":
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        gc.GreetMessage = value;
                        prop = "GreetMessage";
                        val = value;
                    }
                    else
                    {
                        await Context.Channel.SendErrorAsync("Value must not be null or empty space.")
                            .ConfigureAwait(false);
                    }

                    break;
                case "greettimeout":
                    if (ulong.TryParse(value, out var greetTimeout))
                    {
                        gc.GreetTimeout = (long) greetTimeout;
                        prop = "GreetTimeout";
                        val = greetTimeout + " seconds";
                    }
                    else
                    {
                        await Context.Channel
                            .SendErrorAsync("Value must be a non-negative integer.")
                            .ConfigureAwait(false);
                    }

                    break;
                case "enablerespects":
                    if (bool.TryParse(value, out var enableRespects))
                    {
                        gc.EnableRespects = enableRespects;
                        prop = "EnableRespects";
                        val = enableRespects.ToString();
                    }
                    else
                    {
                        await Context.Channel.SendErrorAsync("Value must be a boolean (`true` or `false`)")
                            .ConfigureAwait(false);
                    }

                    break;
                case "invitefiltering":
                    if (bool.TryParse(value, out var inviteFiltering))
                    {
                        gc.InviteFiltering = inviteFiltering;
                        prop = "InviteFiltering";
                        val = inviteFiltering.ToString();
                    }
                    else
                    {
                        await Context.Channel.SendErrorAsync("Value must be a boolean (`true` or `false`)")
                            .ConfigureAwait(false);
                    }

                    break;
                case "phraseminlength":
                    if (ulong.TryParse(value, out var phraseMinLength)
                        && phraseMinLength > 0)
                    {
                        gc.PhraseMinLength = (long) phraseMinLength;
                        prop = "PhraseMinLength";
                        val = $"{phraseMinLength} character(s)";
                    }
                    else
                    {
                        await Context.Channel.SendErrorAsync("Value must be a non-negative integer greater than 0.")
                            .ConfigureAwait(false);
                    }

                    break;
                default:
                    await Context.Channel
                        .SendErrorAsync(
                            $"Property not found with that id. Use `{Config.BotPrefix}sgc` to view valid properties.")
                        .ConfigureAwait(false);
                    return;
            }

            eb.WithOkColor()
                .WithDescription($"Set property **{prop}** to value {val}");
            await _db.UpdateAsync(gc).ConfigureAwait(false);
            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        [Command("testgreet")]
        [Summary("Test the bot's greeting functionality in the current channel.")]
        [Usage("{p}testgreet")]
        [RequirePermRole]
        [RequireContext(ContextType.Guild)]
        private async Task TestGreetAsync()
        {
            if (!(Context.User is SocketGuildUser user)) return;
            var gc = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle($"Welcome to {user.Guild.Name}, {user}!")
                .WithThumbnailUrl(user.AvatarUrl())
                .WithDescription(gc.GreetMessage.Replace("{user}", user.Mention));
            //"Please be sure to carefully read over our rules, and feel free to add a reaction to the message above to add some class roles to yourself!\nEnjoy your stay!");
            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);

            //var _ = Context.Channel.SendMessageAsync(gc.MentionUserOnJoin ? user.Mention : string.Empty, TimeSpan.FromSeconds(gc.GreetTimeout), embed: eb.Build()).ConfigureAwait(false);
        }
    }
}
