using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Administrator.Common;
using Administrator.Common.Attributes;
using Administrator.Common.Database.Models;
using Administrator.Extensions;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Administrator.Modules.Permissions
{
    [Name("Permissions")]
    [RequirePermissionsPass]
    [RequirePermRole]
    [RequireContext(ContextType.Guild)]
    public class PermissionCommands : AdminBase
    {
        public CommandService Commands { get; set; }

        [Command("addpermission")]
        [Alias("addperm", "ap")]
        [Summary("Add a permission to the permission list.\n" +
                 "See **Usage** for examples.")]
        [Usage("ap commandname disable @SomeUser", "ap modulename disable #somechannel",
            "ap commandname enable 0123456789", "ap modulename enable \"Some Role\"")]
        [Remarks("Server owners always bypass all permissions. Keep this in mind in case something breaks.")]
        private Task<RuntimeResult> AddPermission(string commandOrModule, Functionality functionality)
            => AddPermission(commandOrModule, PermissionFilter.Guild, Context.Guild.Id, functionality);

        [Command("addpermission")]
        [Alias("addperm", "ap")]
        [Summary("Add a permission to the permission list.\n" +
                 "See **Usage** for examples.")]
        [Usage("ap commandname disable @SomeUser", "ap modulename disable #somechannel",
            "ap commandname enable 0123456789", "ap modulename enable \"Some Role\"")]
        [Remarks("Server owners always bypass all permissions. Keep this in mind in case something breaks.")]
        private Task<RuntimeResult> AddPermission(string commandOrModule, Functionality functionality,
            [Remainder] SocketGuildUser user)
            => AddPermission(commandOrModule, PermissionFilter.User, user.Id, functionality);

        [Command("addpermission")]
        [Alias("addperm", "ap")]
        [Summary("Add a permission to the permission list.\n" +
                 "See **Usage** for examples.")]
        [Usage("ap commandname disable @SomeUser", "ap modulename disable #somechannel",
            "ap commandname enable 0123456789", "ap modulename enable \"Some Role\"")]
        [Remarks("Server owners always bypass all permissions. Keep this in mind in case something breaks.")]
        private Task<RuntimeResult> AddPermissionAsync(string commandOrModule, Functionality functionality, SocketTextChannel channel)
            => AddPermission(commandOrModule, PermissionFilter.Channel, channel.Id, functionality);

        [Command("addpermission")]
        [Alias("addperm", "ap")]
        [Summary("Add a permission to the permission list.\n" +
                 "See **Usage** for examples.")]
        [Usage("ap commandname disable @SomeUser", "ap modulename disable #somechannel",
            "ap commandname enable 0123456789", "ap modulename enable \"Some Role\"")]
        [Remarks("Server owners always bypass all permissions. Keep this in mind in case something breaks.")]
        private Task<RuntimeResult> AddPermission(string commandOrModule, Functionality functionality, [Remainder] string roleName)
        {
            if (Context.Guild.Roles.FirstOrDefault(x => x.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase)) is
                SocketRole role)
            {
                return AddPermission(commandOrModule, PermissionFilter.Role, role.Id, functionality);
            }

            return CommandError("Role not found.", "Could not find a role by that name.");
        }

        private Task<RuntimeResult> AddPermission(string commandOrModule, PermissionFilter filter, ulong? typeId, Functionality functionality)
        {
            PermissionType type;
            string cmdOrMod;
            var isCommand = false;
            var regex = new Regex(Regex.Escape(Context.Database.GetPrefixOrDefault(Context.Guild)));

            if (Commands.Modules.FirstOrDefault(x => x.Name.Equals(commandOrModule, StringComparison.OrdinalIgnoreCase)) is ModuleInfo module)
            {
                cmdOrMod = module.Name.ToLower();
                type = PermissionType.Module;
            }
            else if (Commands.Commands.FirstOrDefault(x => x.Aliases.Any(y =>
                    y.Equals(regex.Replace(commandOrModule, string.Empty, 1), StringComparison.OrdinalIgnoreCase))) is CommandInfo command)
            {
                isCommand = true;
                cmdOrMod = command.Name.ToLower();
                type = PermissionType.Command;
            }
            else
            {
                return CommandError("Command or module not found.",
                    "Could not find a command or module by that name.");
            }

            var ent = Context.Database.Add(new Permission
            {
                CommandOrModule = cmdOrMod,
                Filter = filter,
                TypeId = typeId,
                Type = type,
                Functionality = functionality,
                GuildId = Context.Guild.Id
            }).Entity;
            Context.Database.SaveChanges();
            var s =
                $"{ent.Functionality}d `{cmdOrMod}` {(isCommand ? "command" : "module")}";
            switch (ent.Filter)
            {
                case PermissionFilter.Guild:
                    s += ".";
                    break;
                case PermissionFilter.Channel:
                    s += $" for channel {Context.Guild.GetTextChannel(ent.TypeId.GetValueOrDefault()).Mention}.";
                    break;
                case PermissionFilter.Role:
                    s += $" for role **{Context.Guild.GetRole(ent.TypeId.GetValueOrDefault())}**.";
                    break;
                case PermissionFilter.User:
                    s += $" for user **{Context.Guild.GetUser(ent.TypeId.GetValueOrDefault())}**.";
                    break;
            }
            return CommandSuccess(embed: new EmbedBuilder()
                .WithOkColor()
                .WithDescription(s)
                .WithModerator(Context.User)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build());
        }

        [Command("removepermission")]
        [Alias("removeperm", "rp")]
        [Summary(
            "Remove a permission from the permission list by ID, or removes the last permission if no ID is specified.")]
        [Usage("rp 10", "rp")]
        private Task<RuntimeResult> RemovePermission(uint? id = null)
        {
            var perms = Context.Database.Permissions.Where(x => x.GuildId == Context.Guild.Id)
                .OrderByDescending(x => x.Id)
                .ToList();

            if (!perms.Any())
            {
                return CommandError("No permissions found on this guild.", "No permissions have been created on this guild.");
            }

            Permission perm = null;

            if (id is null)
            {
                perm = perms.FirstOrDefault();
            }
            else if (perms.FirstOrDefault(x => x.Id == id) is Permission p)
            {
                perm = p;
            }
            
            if (perm is null)
            {
                return CommandError("Permission not found by ID.",
                    "No permission found by that ID.");
            }

            Context.Database.Remove(perm);

            string s;
            switch (perm.Filter)
            {
                case PermissionFilter.Channel:
                    s = $"#{Context.Guild.GetTextChannel(perm.TypeId.GetValueOrDefault())?.Name ?? "UNKNOWN_CHANNEL"}";
                    break;
                case PermissionFilter.Role:
                    s = Context.Guild.GetRole(perm.TypeId.GetValueOrDefault())?.Name ?? "UNKNOWN_ROLE";
                    break;
                case PermissionFilter.User:
                    s = Context.Guild.GetUser(perm.TypeId.GetValueOrDefault())?.ToString() ?? "UNKNOWN_USER";
                    break;
                default:
                    s = string.Empty;
                    break;
            }

            return CommandSuccess(embed: new EmbedBuilder()
                .WithWarnColor()
                .WithTitle($"Permission #{perm.Id} removed.")
                .WithDescription(
                    $"```\n{Context.Database.GetPrefixOrDefault(Context.Guild)}ap {perm.CommandOrModule} {perm.Functionality.ToString().ToLower()}{s}\n```")
                .WithModerator(Context.User)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build());
        }

        [Command("listpermissions")]
        [Alias("listperms", "lp")]
        [Summary(
            "List the current permission list (20 per page). Supply a page number to view additional permissions.")]
        [Usage("lp")]
        private Task<RuntimeResult> ListPermissions(uint page = 1)
        {
            var perms = Context.Database.Permissions.Where(x => x.GuildId == Context.Guild.Id).OrderBy(x => x.Id).ToList();

            if (!perms.Any())
            {
                return CommandError("No permissions found on this guild.", "No permissions have been created on this guild.");
            }

            var totalPages = Math.Ceiling(perms.Count / 20D);
            if (page > totalPages)
            {
                return CommandError($"No permissions found on page {page}",
                    "No permissions found on that page.");
            }

            perms = perms.Skip(((int) page - 1) * 20).Take(20).ToList();
            var s = string.Empty;
            foreach (var perm in perms)
            {
                s +=
                    $"**{perm.Id}.** {Context.Database.GetPrefixOrDefault(Context.Guild)}ap {perm.CommandOrModule} {perm.Functionality.ToString().ToLower()} ";
                switch (perm.Filter)
                {
                    case PermissionFilter.Channel:
                        s += Context.Guild.GetTextChannel(perm.TypeId.GetValueOrDefault())?.Mention ?? "UNKNOWN_CHANNEL";
                        break;
                    case PermissionFilter.Role:
                        s += Context.Guild.GetRole(perm.TypeId.GetValueOrDefault())?.Name ?? "UNKNOWN_ROLE";
                        break;
                    case PermissionFilter.User:
                        s += Context.Guild.GetUser(perm.TypeId.GetValueOrDefault())?.Mention ?? "UNKNOWN_USER";
                        break;
                    default:
                        s += string.Empty;
                        break;
                }

                s += "\n";
            }

            return CommandSuccess(embed: new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Permissions list")
                .WithDescription(s)
                .WithFooter($"{page}/{totalPages}")
                .Build());
        }
    }
}