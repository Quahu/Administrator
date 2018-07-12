using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Administrator.Common.Database;
using Administrator.Common.Database.Models;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using MoreLinq;

namespace Administrator.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class RequirePermissionsPassAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (!(context.User is SocketGuildUser user)) return Task.FromResult(PreconditionResult.FromSuccess());
            if (user.Guild.OwnerId == user.Id) return Task.FromResult(PreconditionResult.FromSuccess());

            using (var scope = services.CreateScope())
            using (var ctx = scope.ServiceProvider.GetService<AdminContext>())
            {
                var perms = ctx.Permissions.Where(x => x.GuildId == user.Guild.Id)
                    .OrderByDescending(x => x.Id).Where(x =>
                        command.Module.Name.Equals(x.CommandOrModule, StringComparison.OrdinalIgnoreCase)
                        || command.Aliases.Any(y => y.Equals(x.CommandOrModule, StringComparison.OrdinalIgnoreCase))).ToList();
                if (!perms.Any()) return Task.FromResult(PreconditionResult.FromSuccess());

                if (perms.FirstOrDefault(x => x.Filter == PermissionFilter.Global) is Permission globalPerm)
                {
                    switch (globalPerm.Functionality)
                    {
                        case Functionality.Disable:
                            return Task.FromResult(PreconditionResult.FromError("Command is disabled globally."));
                        case Functionality.Enable:
                            break;
                    }
                }

                var ids = new[] {user.Id}
                    .Concat(user.Guild.Id)
                    .Concat(context.Channel.Id)
                    .Concat(user.Roles.Where(x => x.Id != user.Guild.EveryoneRole.Id).Select(x => x.Id).ToList())
                    .ToList();

                if (!(perms.FirstOrDefault(x => ids.Contains(x.TypeId.GetValueOrDefault())) is Permission perm))
                    return Task.FromResult(PreconditionResult.FromSuccess());

                return perm.Functionality == Functionality.Enable
                    ? Task.FromResult(PreconditionResult.FromSuccess())
                    : Task.FromResult(PreconditionResult.FromError($"Permission #{perm.Id} blocked command from executing."));
            }
        }
    }
}