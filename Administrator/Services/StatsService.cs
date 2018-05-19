using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Discord.WebSocket;

namespace Administrator.Services
{
    public class StatsService
    {
        private const string BOT_VERSION = "1.1.2";
        private readonly BaseSocketClient _client;
        private readonly DateTimeOffset _dt;

        public StatsService(BaseSocketClient client)
        {
            _client = client;
            _dt = DateTimeOffset.UtcNow;
        }

        public TimeSpan Uptime
            => DateTimeOffset.UtcNow - _dt;

        public ulong MessagesReceived { get; set; }

        public ulong CommandsRun { get; set; }

        public int Guilds
            => _client.Guilds.Count;

        public int Users
            => _client.Guilds.Select(g => g.MemberCount).Sum();

        public int TextChannels
            => _client.Guilds.SelectMany(g => g.TextChannels).Count();

        public int VoiceChannels
            => _client.Guilds.SelectMany(g => g.VoiceChannels).Count();

        public string BotVersion
            => BOT_VERSION;
    }
}
