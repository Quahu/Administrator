using Administrator.Common;
using Administrator.Extensions;
using Administrator.Services.Database;
using Administrator.Services.Database.Models;
using Discord;
using Discord.WebSocket;
using NLog;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Administrator.Services.Scheduler.Schedules
{
    public class LtpChecker
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static readonly Config Config = BotConfig.New();
        private readonly DbService _db;
        private readonly BaseSocketClient _client;

        public LtpChecker(DbService db, BaseSocketClient client)
        {
            _db = db;
            _client = client;
        }

        public async Task RemoveExpiredLtpPlayersAsync()
        {
            var guildConfigs = await _db.GetAsync<GuildConfig>().ConfigureAwait(false);
            var ltpUsers = await _db.GetAsync<LtpUser>(IsExpired).ConfigureAwait(false);

            foreach (var ltpUser in ltpUsers)
            {
                if (_client.GetGuild((ulong) ltpUser.GuildId).GetUser((ulong) ltpUser.UserId) is SocketGuildUser user)
                {
                    try
                    {
                        var gc = await _db.GetOrCreateGuildConfigAsync(user.Guild).ConfigureAwait(false);
                        if (user.Roles.FirstOrDefault(r => r.Id == (ulong) gc.LookingToPlayRole) is SocketRole toRemove)
                        {
                            await user.RemoveRoleAsync(toRemove).ConfigureAwait(false);
                            await _db.DeleteAsync(ltpUser).ConfigureAwait(false);
                            Log.ConditionalDebug("Looking to Play role removed.");

                            var eb = new EmbedBuilder()
                                .WithOkColor()
                                .WithDescription(
                                    $"Hey, **{user}**! Your Looking to Play role has been removed due to expiring.\nTo add it back, simply type `{Config.BotPrefix}ltp` in a channel in {user.Guild.Name} to gain the role again.");
                            var dm = await user.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                            await dm.EmbedAsync(eb.Build()).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(ex, ex.ToString);
                    }
                }
            }

            bool IsExpired(LtpUser ltp)
                => DateTimeOffset.UtcNow > ltp.Expires;
        }
    }
}
