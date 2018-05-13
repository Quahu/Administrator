using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Administrator.Services.Database;
using Administrator.Services.Database.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace Administrator.Extensions.Attributes
{
    public sealed class RequirePermissionsPass : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context,
            CommandInfo command, IServiceProvider services)
        {
            if (context.Channel is IPrivateChannel) return PreconditionResult.FromSuccess();

            var db = services.GetService<DbService>();

            var permissions = await db.GetAsync<Permission>(x => x.GuildId == (long) context.Guild.Id).ConfigureAwait(false);
            permissions = permissions.OrderByDescending(x => x.Id).ToList();
            var ids = new List<ulong>
            {
                context.Message.Author.Id,
                context.Channel.Id,
                context.Guild.Id
            };
            if (!(context.Message.Author is SocketGuildUser user)) return PreconditionResult.FromError("User is not SocketGuildUser");
            ids.AddRange(user.Roles.OrderByDescending(x => x.Position)
                .Select(x => x.Id));

            foreach (var perm in permissions)
                if (ids.Contains((ulong) perm.Id) &&
                    (command.Aliases.Contains(perm.CommandName) || perm.CommandName.Equals("all")))
                    switch (perm.Type)
                    {
                        case PermissionType.Disable:
                            return PreconditionResult.FromError(
                                $"Permission #{perm.Id} blocked user [{context.Message.Author.Id}] from executing command");
                        case PermissionType.Enable:
                            return PreconditionResult.FromSuccess();
                    }
            // no perms for these ids
            return PreconditionResult.FromSuccess();
        }
    }
}