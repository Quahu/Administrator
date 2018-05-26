using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Administrator.Extensions;
using Administrator.Services.Database;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NLog;

namespace Administrator.Modules.Administration.Services
{
    public class ChannelLockService
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static readonly OverwritePermissions Allowed = new OverwritePermissions(sendMessages: PermValue.Deny, addReactions: PermValue.Deny);
        private static readonly OverwritePermissions Unallowed = new OverwritePermissions(sendMessages: PermValue.Allow, addReactions: PermValue.Allow);
        private readonly DbService _db;
        
        public Dictionary<ulong, List<Overwrite>> LockedChannels { get; set; }

        public ChannelLockService(DbService db)
        {
            LockedChannels = new Dictionary<ulong, List<Overwrite>>();
            _db = db;
        }

        public async Task LockChannelAsync(SocketCommandContext context, ITextChannel channel)
        {
            var gc = await _db.GetOrCreateGuildConfigAsync(context.Guild).ConfigureAwait(false);
            if (!(context.Guild.Roles.FirstOrDefault(x => x.Id == (ulong) gc.PermRole) is SocketRole permRole))
            {
                // this shouldn't happen, but let's check anyways
                await context.Channel.SendErrorAsync("This guild's permrole is not set.").ConfigureAwait(false);
                return;
            }

            try
            {
                var perms = channel.PermissionOverwrites.ToList();
                // remove old perms
                foreach (var o in perms)
                {
                    switch (o.TargetType)
                    {
                        case PermissionTarget.Role:
                            if (context.Guild.Roles.FirstOrDefault(x => x.Id == o.TargetId) is SocketRole r)
                                await channel.RemovePermissionOverwriteAsync(r).ConfigureAwait(false);
                            break;
                        case PermissionTarget.User:
                            if (context.Guild.GetUser(o.TargetId) is SocketGuildUser u)
                                await channel.RemovePermissionOverwriteAsync(u).ConfigureAwait(false);
                            break;
                    }
                }

                // add lock perms
                await channel.AddPermissionOverwriteAsync(context.Guild.EveryoneRole, Unallowed).ConfigureAwait(false);
                await channel.AddPermissionOverwriteAsync(permRole, Allowed).ConfigureAwait(false);
                
                await channel.SendConfirmAsync($"{channel.Mention} is now locked.").ConfigureAwait(false);
                
                LockedChannels.Add(channel.Id, perms);
            }
            catch
            {
                await context.Channel.SendErrorAsync("An error occurred setting up lock permissions.")
                    .ConfigureAwait(false);
            }
        }

        public async Task UnlockChannelAsync(SocketCommandContext context, ITextChannel channel)
        {
            try
            {
                if (!LockedChannels.TryGetValue(channel.Id, out var perms)) return;

                // remove lock perms
                foreach (var o in channel.PermissionOverwrites.ToList())
                {
                    switch (o.TargetType)
                    {
                        case PermissionTarget.Role:
                            if (context.Guild.Roles.FirstOrDefault(x => x.Id == o.TargetId) is SocketRole r)
                                await channel.RemovePermissionOverwriteAsync(r).ConfigureAwait(false);
                            break;
                        case PermissionTarget.User:
                            if (context.Guild.GetUser(o.TargetId) is SocketGuildUser u)
                                await channel.RemovePermissionOverwriteAsync(u).ConfigureAwait(false);
                            break;
                    }
                }

                // add old perms
                foreach (var p in perms)
                {
                    switch (p.TargetType)
                    {
                        case PermissionTarget.Role:
                            if (context.Guild.Roles.FirstOrDefault(x => x.Id == p.TargetId) is SocketRole r)
                                await channel.AddPermissionOverwriteAsync(r, p.Permissions).ConfigureAwait(false);
                            break;
                        case PermissionTarget.User:
                            if (context.Guild.GetUser(p.TargetId) is SocketGuildUser u)
                                await channel.AddPermissionOverwriteAsync(u, p.Permissions).ConfigureAwait(false);
                            break;
                    }
                }

                await channel.SendConfirmAsync($"{channel.Mention} is now unlocked.").ConfigureAwait(false);

                LockedChannels.Remove(channel.Id);
            }
            catch
            {
                await context.Channel.SendErrorAsync("An error occurred removing lock permissions.")
                    .ConfigureAwait(false);
            }
        }
    }
}