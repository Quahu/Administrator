using Administrator.Common;
using Administrator.Services.Database;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Administrator.Extensions.Attributes
{
    public sealed class RequirePermRole : PreconditionAttribute
    {
        private static readonly Config Config = BotConfig.New();

        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context,
            CommandInfo command, IServiceProvider services)
        {
            var db = services.GetService<DbService>();

            var guildConfig = await db.GetOrCreateGuildConfigAsync(context.Guild).ConfigureAwait(false);
            var permRole = context.Guild.Roles.FirstOrDefault(x => x.Id == (ulong) guildConfig.PermRole);

            if (permRole is null)
            {
                var eb = new EmbedBuilder()
                    .WithErrorColor()
                    .WithDescription(
                        $"This guild has not set up their permrole yet!\nUse `{Config.BotPrefix}permrole Your PermRole Here` to set it.")
                    .WithFooter("Contact a user with Administrator permissions or the guild owner.");
                await context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
                return PreconditionResult.FromError("Guild has not set up permrole.");
            }

            if (!(context.User is SocketGuildUser user))
                return PreconditionResult.FromError("Internal error. Please report this.");

            if (user.Roles.Any(x => x.Id == permRole.Id))
            {
                return PreconditionResult.FromSuccess();
            }

            await context.Channel.SendErrorAsync("You do not have the guild's permrole!").ConfigureAwait(false);
            return PreconditionResult.FromError("User does not have permrole.");
        }
    }
}