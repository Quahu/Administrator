using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace Administrator.Services
{
    public static class StatsService
    {
        public const string BOT_VERSION = "2.0-pre";
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static DateTimeOffset _startTime;
        private static DiscordSocketClient _client;

        public static void Initialize(IServiceProvider services)
        {
            _startTime = DateTimeOffset.UtcNow;
            _client = services.GetRequiredService<DiscordSocketClient>();
            CommandsExecuted = new Dictionary<ulong, uint>();
            MessagesReceived = new Dictionary<ulong, uint>();
            Log.Info("Initialized.");
        }

        public static Dictionary<ulong, uint> CommandsExecuted { get; private set; }

        public static Dictionary<ulong, uint> MessagesReceived { get; private set; }

        public static uint GetMessagesReceived(IGuild guild)
            => MessagesReceived.ContainsKey(guild?.Id ?? 0) ? MessagesReceived[guild?.Id ?? 0] : 0;

        public static uint GetCommandsExecuted(IGuild guild)
            => CommandsExecuted.ContainsKey(guild?.Id ?? 0) ? CommandsExecuted[guild?.Id ?? 0] : 0;

        public static void IncrementCommandsExecuted(IGuild guild)
        {
            if (CommandsExecuted.ContainsKey(guild?.Id ?? 0))
            {
                CommandsExecuted[guild?.Id ?? 0]++;
                return;
            }

            CommandsExecuted.Add(guild?.Id ?? 0, 1);
        }

        public static void IncrementMessagesReceived(IGuild guild)
        {
            if (MessagesReceived.ContainsKey(guild?.Id ?? 0))
            {
                MessagesReceived[guild?.Id ?? 0]++;
                return;
            }

            MessagesReceived.Add(guild?.Id ?? 0, 1);
        }

        public static int TotalGuilds
            => _client.Guilds.Count;

        public static int TotalUsers
            => _client.Guilds.Sum(x => x.MemberCount);

        public static int TotalTextChannels
            => _client.Guilds.Sum(x => x.TextChannels.Count);

        public static int TotalVoiceChannels
            => _client.Guilds.Sum(x => x.VoiceChannels.Count);

        public static TimeSpan Uptime
            => DateTimeOffset.UtcNow - _startTime;

        public static long GetTotalMemoryUsage()
        {
            using (var process = Process.GetCurrentProcess())
            {
                return process.PrivateMemorySize64;
            }
        }

        public static async Task<IUser> GetOwnerAsync()
        {
            var app = await _client.GetApplicationInfoAsync();
            return app.Owner;
        }
    }
}
