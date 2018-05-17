using Administrator.Services.Database.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NLog;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Administrator.Services.Database
{
    public class DbService
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static SQLiteAsyncConnection _conn;
        private readonly DiscordSocketClient _client;

        public DbService(string connectionString, DiscordSocketClient client, CommandService commands)
        {
            _conn = new SQLiteAsyncConnection(connectionString);
            _client = client;

            client.Ready += OnReady;
        }

        #region Setup

        private async Task OnReady()
        {
            try
            {
                await _conn.CreateTablesAsync<AntiSpamChannel, GuildConfig, Suggestion, Phrase, MutedUser>().ConfigureAwait(false);
                await _conn.CreateTablesAsync<Warning, WarningPunishment, ModNote, UserPhrase, Permission>()
                    .ConfigureAwait(false);
                await _conn.CreateTablesAsync<LtpUser, Respects, ReactionRoleMessage, BlacklistedPhrase, BlacklistedWord>().ConfigureAwait(false);

                var newAntiSpamChannels = new List<AntiSpamChannel>();
                var newWarningPunishments = new List<WarningPunishment>();

                var currentWarningPunishments = await GetAsync<WarningPunishment>().ConfigureAwait(false);

                foreach (var guild in _client.Guilds)
                {
                    var gc = await GetOrCreateGuildConfigAsync(guild).ConfigureAwait(false);
                    if (gc.HasModifiedWarningPunishments ||
                        currentWarningPunishments.Any(x => x.GuildId == (long) guild.Id)) continue;
                    newWarningPunishments.Add(new WarningPunishment
                    {
                        GuildId = (long) guild.Id,
                        Count = 2,
                        PunishmentId = (long) Punishment.Mute
                    });
                    newWarningPunishments.Add(new WarningPunishment
                    {
                        GuildId = (long) guild.Id,
                        Count = 3,
                        PunishmentId = (long) Punishment.Softban
                    });
                    newWarningPunishments.Add(new WarningPunishment
                    {
                        GuildId = (long) guild.Id,
                        Count = 5,
                        PunishmentId = (long) Punishment.Ban
                    });
                    gc.HasModifiedWarningPunishments = true;
                    await UpdateAsync(gc).ConfigureAwait(false);
                }

                if (newAntiSpamChannels.Count > 0) await InsertAllAsync(newAntiSpamChannels).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warn(ex, ex.Message);
                Log.Warn(ex, ex.StackTrace);
            }

            _client.Ready -= OnReady;
        }

        #endregion

        #region Generics

        public async Task<long> InsertAsync<T>(T data) where T : IDbModel
        {
            try
            {
                return await _conn.InsertAsync(data).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warn(ex, ex.ToString);
            }

            return -1;
        }

        public async Task<long> InsertAllAsync<T>(IEnumerable<T> data) where T : IDbModel
        {
            try
            {
                return await _conn.InsertAllAsync(data).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warn(ex, ex.ToString);
            }

            return -1;
        }

        public async Task<IReadOnlyCollection<T>> GetAsync<T>(Func<T, bool> where = null) where T : IDbModel, new()
        {
            try
            {
                if (where is null)
                {
                    return await _conn.Table<T>().ToListAsync().ConfigureAwait(false);
                }

                var ts = await _conn.Table<T>().ToListAsync().ConfigureAwait(false);
                return ts.Where(where).ToList();
            }
            catch (Exception ex)
            {
                Log.Warn(ex, ex.ToString);
            }

            return null;
        }

        public async Task<long> UpdateAsync<T>(T data) where T : IDbModel, new()
        {
            try
            {
                return await _conn.UpdateAsync(data).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warn(ex, ex.ToString);
            }

            return -1;
        }

        public async Task<long> DeleteAsync<T>(long pk) where T : IDbModel
        {
            try
            {
                return await _conn.DeleteAsync<T>(pk).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warn(ex, ex.ToString);
            }

            return -1;
        }

        public async Task<long> DeleteAsync<T>(T data) where T : IDbModel
        {
            try
            {
                return await _conn.DeleteAsync(data).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warn(ex, ex.ToString);
            }

            return -1;
        }

        public async Task DeleteAllAsync<T>(IEnumerable<T> data) where T : IDbModel, new()
        {
            try
            {
                foreach (var d in data)
                {
                    await _conn.DeleteAsync(d).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log.Warn(ex, ex.ToString);
            }
        }

        public async Task DeleteAllExceptAsync<T>(Func<T, bool> except) where T : IDbModel, new()
        {
            try
            {
                var ts = await GetAsync<T>().ConfigureAwait(false);
                var keep = ts.Where(except);
                await _conn.DeleteAllAsync<T>().ConfigureAwait(false);
                await InsertAllAsync(keep).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warn(ex, ex.ToString);
            }
        }

        #endregion

        public async Task<GuildConfig> GetOrCreateGuildConfigAsync(IGuild guild)
        {
            try
            {
                var gcs = await GetAsync<GuildConfig>(x => x.Id == (long) guild.Id).ConfigureAwait(false);
                if (gcs.FirstOrDefault() is GuildConfig gc) return gc;
                Log.Warn($"No guild config found for {guild}. Creating.");
                var newGc = new GuildConfig
                {
                    Id = (long) guild.Id
                };
                await InsertAsync(newGc).ConfigureAwait(false);
                return newGc;
            }
            catch (Exception ex)
            {
                Log.Warn(ex, ex.ToString);
            }

            return null;
        }
    }
}