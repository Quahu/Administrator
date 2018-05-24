using System;
using System.Linq;
using System.Threading.Tasks;
using Administrator.Extensions;
using Administrator.Extensions.Attributes;
using Administrator.Services;
using Administrator.Services.Database;
using Administrator.Services.Database.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Administrator.Modules.Permissions
{
    [Name("Permissions")]
    public class PermissionsCommands : ModuleBase<SocketCommandContext>
    {
        private static readonly Config Config = BotConfig.New();
        private readonly DbService db;

        public PermissionsCommands(DbService db)
        {
            this.db = db;
        }

        [Command("addpermission")]
        [Alias("addperm", "ap")]
        [Summary("Add a permission to the permission list.")]
        [Usage("{p}ap {p}phrase disable #some-channel", "{p}ap all enable Some Role")]
        [RequirePermRole]
        [RequireContext(ContextType.Guild)]
        [Priority(3)]
        private async Task AddPermissionAsync(string cmd, string type, [Remainder] ITextChannel channel)
        {
            if (channel is SocketTextChannel c && Context.Guild.TextChannels.Any(x => x.Id == c.Id))
            {
                await InternalAddPermissionAsync(cmd, type, (long)c.Id).ConfigureAwait(false);
            }
        }

        [Command("addpermission")]
        [Alias("addperm", "ap")]
        [Summary("Add a permission to the permission list.")]
        [Usage("{p}ap {p}phrase disable #some-channel", "{p}ap all enable Some Role")]
        [RequirePermRole]
        [RequireContext(ContextType.Guild)]
        [Priority(2)]
        private async Task AddPermissionAsync(string cmd, string type, [Remainder] IUser user)
        {
            if (Context.Guild.Users.FirstOrDefault(x => x.Id == user.Id) is SocketGuildUser u)
            {
                await InternalAddPermissionAsync(cmd, type, (long) u.Id).ConfigureAwait(false);
            }
        }

        [Command("addpermission")]
        [Alias("addperm", "ap")]
        [Summary("Add a permission to the permission list.")]
        [Usage("{p}ap {p}phrase disable #some-channel", "{p}ap all enable Some Role")]
        [RequirePermRole]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        private async Task AddPermissionAsync(string cmd, string type, [Remainder] string roleName)
        {
            if (Context.Guild.Roles.FirstOrDefault(x =>
                x.Name.Equals(roleName, StringComparison.InvariantCultureIgnoreCase)) is SocketRole role)
            {
                await InternalAddPermissionAsync(cmd, type, (long) role.Id).ConfigureAwait(false);
            }
        }

        private async Task InternalAddPermissionAsync(string cmd, string type, long id)
        {
            type = type[0].ToString().ToUpper() + type.Substring(1).ToLower();
            if (Enum.TryParse(type, out PermissionType t))
            {
                var perm = new Permission
                {
                    GuildId = (long) Context.Guild.Id,
                    CommandName = cmd.TrimStart(Config.BotPrefix.ToArray()),
                    SetId = id,
                    Type = t
                };

                var description = $"```\n{"Guild/Channel/Role/User",27} | {"Command",12} | {"Type",7}\n"
                                  + "▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬\n";
                var cmdName = (Config.BotPrefix + perm.CommandName).Equals($"{Config.BotPrefix}all")
                    ? "*"
                    : Config.BotPrefix + perm.CommandName;
                var name = perm.SetId.ToString();
                if (Context.Client.GetGuild((ulong) perm.SetId) is SocketGuild guild)
                    name = guild.Name;
                else if (Context.Guild.GetTextChannel((ulong) perm.SetId) is SocketTextChannel chnl)
                    name = $"#{chnl.Name}";
                else if (Context.Guild.GetRole((ulong) perm.SetId) is SocketRole role)
                    name = role.Name;
                else if (Context.Guild.GetUser((ulong) perm.SetId) is SocketGuildUser user)
                    name = user.ToString();
                description += $"{name,27} | {cmdName,12} | {perm.Type,7}\n```";

                if (await db.InsertAsync(perm) > 0)
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle("New permission added.")
                        .WithDescription(description)
                        .Build()).ConfigureAwait(false);
                }
                else
                {
                    await Context.Channel.SendErrorAsync("Could not add a permission given those inputs.")
                        .ConfigureAwait(false);
                }
            }
            else
            {
                await Context.Channel.SendErrorAsync("Could not parse the permission type.").ConfigureAwait(false);
            }
        }

        [Command("listpermissions")]
        [Alias("listperms", "lp")]
        [Summary("Display the permission list.")]
        [Usage("{p}lp")]
        [RequirePermRole]
        [RequireContext(ContextType.Guild)]
        private async Task ListPermissionsAsync(int page = 1)
        {
            var permissions = await db.GetAsync<Permission>(x => x.GuildId == (long) Context.Guild.Id).ConfigureAwait(false);

            if (!permissions.Any())
            {
                await Context.Channel.SendErrorAsync("No permissions found for this guild.").ConfigureAwait(false);
                return;
            }

            if (permissions.Count < (page - 1) * 15)
            {
                await Context.Channel.SendErrorAsync("No permissions found on this page.").ConfigureAwait(false);
                return;
            }

            var perms = permissions.Skip((page - 1) * 15).Take(15).ToList();
            var description = $"```\n{"Id",5} | {"Guild/Channel/Role/User",27} | {"Command",12} | {"Type",7}\n"
                              + "▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬\n";
            foreach (var p in perms)
            {
                var cmdName = (Config.BotPrefix + p.CommandName).Equals($"{Config.BotPrefix}all")
                    ? "*"
                    : Config.BotPrefix + p.CommandName;
                var name = p.SetId.ToString();
                if (Context.Client.GetGuild((ulong) p.SetId) is SocketGuild guild)
                    name = guild.Name;
                else if (Context.Guild.GetTextChannel((ulong) p.SetId) is SocketTextChannel chnl)
                    name = $"#{chnl.Name}";
                else if (Context.Guild.GetRole((ulong) p.SetId) is SocketRole role)
                    name = role.Name;
                else if (Context.Guild.GetUser((ulong) p.SetId) is SocketGuildUser user)
                    name = user.ToString();
                description += $"{p.Id,5} | {name,27} | {cmdName,12} | {p.Type,7}\n";
            }

            description += "```";
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Permissions list:")
                .WithDescription(description)
                .WithFooter($"Page {page} / {Math.Ceiling(permissions.Count / 15.0)}");

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        
        [Command("removepermission")]
        [Alias("remperm", "rp")]
        [Summary("Removes a permission given its ID.")]
        [Usage("{p}rp 5")]
        [Remarks("Use {p}lp to see permission IDs.")]
        [RequirePermRole]
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        private async Task RemovePermissionAsync(long permId)
        {
            var perms = await db.GetAsync<Permission>(x => x.GuildId == (long) Context.Guild.Id && x.Id == permId).ConfigureAwait(false);

            if (perms.FirstOrDefault() is Permission p)
            {
                var description = $"```\n{"Id",5} | {"Guild/Channel/Role/User",27} | {"Command",12} | {"Type",7}\n"
                                  + "▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬\n";
                var cmdName = (Config.BotPrefix + p.CommandName).Equals($"{Config.BotPrefix}all")
                    ? "*"
                    : Config.BotPrefix + p.CommandName;
                var name = p.SetId.ToString();
                if (Context.Client.GetGuild((ulong) p.SetId) is SocketGuild guild)
                    name = guild.Name;
                else if (Context.Guild.GetTextChannel((ulong) p.SetId) is SocketTextChannel chnl)
                    name = $"#{chnl.Name}";
                else if (Context.Guild.GetRole((ulong) p.SetId) is SocketRole role)
                    name = role.Name;
                else if (Context.Guild.GetUser((ulong) p.SetId) is SocketGuildUser user)
                    name = user.ToString();
                description += $"{p.Id,5} | {name,27} | {cmdName,12} | {p.Type,7}\n```";

                await db.DeleteAsync(p).ConfigureAwait(false);
                await Context.Channel.EmbedAsync(new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Permission removed.")
                    .WithDescription(description)
                    .Build()).ConfigureAwait(false);
            }
            else
            {
                await Context.Channel.SendErrorAsync("Permission not found with that ID.").ConfigureAwait(false);
            }
        }

        [Command("addblacklistedword")]
        [Alias("addblw", "ablw")]
        [Summary("Add a blacklisted word or words to the guild.\n" +
                 "Any messages sent containing the word will automatically be deleted.\n" +
                 "Users with the **PermRole** are immune to word blacklisting.")]
        [Usage("{p}ablw ass")]
        [RequireContext(ContextType.Guild)]
        [RequirePermRole]
        private async Task AddBlacklistedWordAsync([Remainder] string word)
        {
            var blw = new BlacklistedWord
            {
                GuildId = (long) Context.Guild.Id,
                Word = word
            };

            var blws = await db.GetAsync<BlacklistedWord>(x => x.GuildId == (long) Context.Guild.Id)
                .ConfigureAwait(false);

            if (blws.Any(x => x.Word.Equals(word, StringComparison.InvariantCultureIgnoreCase)))
            {
                await Context.Channel.SendErrorAsync("That blacklisted word already exists on that guild!")
                    .ConfigureAwait(false);
                return;
            }

            await db.InsertAsync(blw).ConfigureAwait(false);

            await Context.Channel.EmbedAsync(new EmbedBuilder()
                .WithOkColor()
                .WithTitle("New blacklisted word added.")
                .WithDescription(
                    $"Any message sent containing the word(s) `{word.ToLower()}` will be deleted immediately by the bot.")
                .Build()).ConfigureAwait(false);
        }

        [Command("blacklistedwords")]
        [Alias("blwords", "blws")]
        [Summary("View this guild's blacklisted words.")]
        [Usage("{p}blws")]
        [RequireContext(ContextType.Guild)]
        [RequirePermRole]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        private async Task GetBlacklistedWordsAsync()
        {
            var blws = await db.GetAsync<BlacklistedWord>(x => x.GuildId == (long) Context.Guild.Id)
                .ConfigureAwait(false);

            if (!blws.Any())
            {
                await Context.Channel.SendErrorAsync("No blacklisted words found for this guild.")
                    .ConfigureAwait(false);
                return;
            }

            await Context.Channel.EmbedAsync(new EmbedBuilder()
                .WithOkColor()
                .WithTitle($"Blacklisted words for {Context.Guild.Name}")
                .WithDescription($"```\n{string.Join("\n", blws.Select(x => $"{x.Id} - {x.Word.ToLower()}"))}\n```")
                .Build()).ConfigureAwait(false);
        }

        [Command("removeblacklistedword")]
        [Alias("remblw", "rblw")]
        [Summary("Remove a blacklisted word or words by ID.\nUse {p}blws to view blacklisted word IDs.")]
        [Usage("{p}rblw 15")]
        [RequireContext(ContextType.Guild)]
        [RequirePermRole]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        private async Task RemoveBlacklistedWordAsync(long id)
        {
            var blws = await db.GetAsync<BlacklistedWord>(x => x.GuildId == (long) Context.Guild.Id && x.Id == id)
                .ConfigureAwait(false);

            if (blws.FirstOrDefault() is BlacklistedWord b)
            {
                await db.DeleteAsync(b).ConfigureAwait(false);
                await Context.Channel.EmbedAsync(new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle($"Blacklisted word with id {id} deleted.")
                    .WithDescription($"Messages sent containing the word(s) `{b.Word}` will no longer be deleted.")
                    .Build()).ConfigureAwait(false);
            }
            else
            {
                await Context.Channel.SendErrorAsync("No blacklisted word found by that ID.").ConfigureAwait(false);
            }
        }
    }
}