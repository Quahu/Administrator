using System;
using System.Threading.Tasks;
using Discord.Commands;

namespace Administrator.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class RequireBotOwnerAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
            => BotConfig.OwnerIds.Contains(context.User.Id) ? Task.FromResult(PreconditionResult.FromSuccess()) : Task.FromResult(PreconditionResult.FromError("This command can only be used by owners of the bot."));
    }
}
