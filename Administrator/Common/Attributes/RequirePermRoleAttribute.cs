using Administrator.Extensions;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using Administrator.Common.Database;

namespace Administrator.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class RequirePermRoleAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (!(context.User is SocketGuildUser user)) return Task.FromResult(PreconditionResult.FromSuccess());

            using (var scope = services.CreateScope())
            using (var ctx = scope.ServiceProvider.GetService<AdminContext>())
            {
                var gc = ctx.GetOrCreateGuildConfig(context.Guild);

                if (gc.PermRoleId.HasValue
                    && context.Guild.GetRole(gc.PermRoleId.Value) is SocketRole permRole)
                {
                    return user.Roles.Any(x => x.Id == permRole.Id) ? Task.FromResult(PreconditionResult.FromSuccess()) : Task.FromResult(PreconditionResult.FromError("User does not have permrole."));
                }

                return Task.FromResult(PreconditionResult.FromError("Guild has not set up permrole."));
            }
        }
    }
}
