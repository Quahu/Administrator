using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Administrator.Services.Database;
using Administrator.Services.Database.Models;
using Discord.Rest;
using Discord.WebSocket;
using FluentScheduler;
using NLog;

namespace Administrator.Services.Scheduler.Schedules
{
    public class MuteChecker
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly DbService _db;
        private readonly BaseSocketClient _client;

        public MuteChecker(DbService db, BaseSocketClient client)
        {
            _db = db;
            _client = client;
        }

        public async Task CheckMutesAsync()
        {
            //log.ConditionalDebug("Checking for mutes to cancel.");
            var mutes = await _db.GetAsync<MutedUser>(IsExpired).ConfigureAwait(false);

            foreach (var mute in mutes)
            {
                if (_client.GetGuild((ulong) mute.GuildId) is SocketGuild guild
                    && guild.GetUser((ulong) mute.UserId) is SocketGuildUser user)
                {
                    var gc = await _db.GetOrCreateGuildConfigAsync(guild).ConfigureAwait(false);
                    if (user.Roles.FirstOrDefault(r => r.Id == (ulong) gc.MuteRole) is SocketRole toRemove)
                    {
                        try
                        {
                            await user.RemoveRoleAsync(toRemove).ConfigureAwait(false);
                            Log.ConditionalDebug("User unmuted.");
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
            }

            bool IsExpired(MutedUser user)
                => DateTimeOffset.UtcNow > user.Ending;
        }
    }
}
