using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Administrator.Common.Attributes
{
    public class RequireGuildOwnerAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (!(context.Guild is SocketGuild guild)) return Task.FromResult(PreconditionResult.FromSuccess());
            return context.User.Id == guild.OwnerId
                ? Task.FromResult(PreconditionResult.FromSuccess())
                : Task.FromResult(PreconditionResult.FromError("This command can only be invoked by the guild owner."));
        }
    }
}
