using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using System.Xml;
using Administrator.Extensions;
using Discord;
using Discord.WebSocket;

namespace Administrator.Services
{
    public class CrosstalkCall
    {
        public SocketTextChannel Channel1 { get; set; }

        public SocketTextChannel Channel2 { get; set; }

        public string ConnectionCode { get; set; }

        public bool IsConnected { get; set; }

        public DateTimeOffset Starting { get; set; } = DateTimeOffset.UtcNow;

        public bool IsExpired
            => DateTimeOffset.UtcNow - Starting > TimeSpan.FromMinutes(2);

        public static string GenerateCode()
            => Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Substring(0, 8).ToLower();

        public bool Equals(CrosstalkCall call)
            => Channel1.Id == call.Channel1.Id
                   && Channel2?.Id == call.Channel2?.Id
                   && ConnectionCode == call.ConnectionCode
                   && IsConnected == call.IsConnected;

        public bool ContainsChannel(SocketTextChannel c)
            => Channel1.Id == c.Id || Channel2?.Id == c.Id;

        public bool ContainsGuild(SocketGuild g)
            => Channel1.Guild.Id == g.Id || Channel2?.Guild.Id == g.Id;
    }

    public class CrosstalkService
    {
        private readonly DiscordSocketClient _client;

        public CrosstalkService(DiscordSocketClient client)
        {
            _client = client;

            Calls = new List<CrosstalkCall>();
        }

        public List<CrosstalkCall> Calls { get; }
        //public List<SocketTextChannel> Channels { get; }

        // with code
        public async Task AddChannelAsync(SocketTextChannel c, bool generateCode, string code = null)
        {
            if (Calls.FirstOrDefault(x => x.ContainsGuild(c.Guild)) is CrosstalkCall inCall)
            {
                if (c.Guild.GetTextChannel(inCall.Channel1.Id) is null)
                {
                    await c.SendErrorAsync($"Your guild is already on the phone in {inCall.Channel2.Mention}!")
                        .ConfigureAwait(false);
                }
                else
                {
                    await c.SendErrorAsync($"Your guild is already on the phone in {inCall.Channel1.Mention}!")
                        .ConfigureAwait(false);
                }

                return;
            }

            // try connecting by connection code
            if (!string.IsNullOrWhiteSpace(code)
                && Calls.FirstOrDefault(x =>
                        !string.IsNullOrWhiteSpace(x.ConnectionCode) && x.ConnectionCode.Equals(code) &&
                        !x.IsConnected) is
                    CrosstalkCall codeCall)
            {
                Calls.Remove(codeCall);
                codeCall.Channel2 = c;
                codeCall.IsConnected = true;
                codeCall.Starting = DateTimeOffset.UtcNow;
                Calls.Add(codeCall);

                await codeCall.Channel1
                    .SendConfirmAsync($"You've connected on the crosstalk line. Say hi to `#{codeCall.Channel2.Name}`!")
                    .ConfigureAwait(false);
                await codeCall.Channel2
                    .SendConfirmAsync($"You've connected on the crosstalk line. Say hi to `#{codeCall.Channel1.Name}`!")
                    .ConfigureAwait(false);

                return;
            }

            // try connecting to the first available channel that is not using a code
            if (Calls.FirstOrDefault(x =>
                    x.Channel1.Id != c.Id && !x.IsConnected && string.IsNullOrWhiteSpace(x.ConnectionCode)) is
                CrosstalkCall
                cl)
            {
                Calls.Remove(cl);
                cl.Channel2 = c;
                cl.IsConnected = true;
                cl.Starting = DateTimeOffset.UtcNow;
                Calls.Add(cl);

                await cl.Channel1
                    .SendConfirmAsync($"You've connected on the crosstalk line. Say hi to `#{cl.Channel2.Name}`!")
                    .ConfigureAwait(false);
                await cl.Channel2
                    .SendConfirmAsync($"You've connected on the crosstalk line. Say hi to `#{cl.Channel1.Name}`!")
                    .ConfigureAwait(false);

                return;
            }

            // no call found, create a new one
            var ringing = new CrosstalkCall
            {
                Channel1 = c,
                ConnectionCode = generateCode ? CrosstalkCall.GenerateCode() : null
            };

            Calls.Add(ringing);

            await c.SendConfirmAsync(
                $"Ringing on the crosstalk phone{(string.IsNullOrWhiteSpace(ringing.ConnectionCode) ? "..." : $" - your code is `{ringing.ConnectionCode}`. Use this code to connect directly from another channel!")}");
        }
    }
}
