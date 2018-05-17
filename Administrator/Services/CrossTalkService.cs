using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Administrator.Extensions;
using Discord;
using Discord.WebSocket;

namespace Administrator.Services
{
    public class CrosstalkCall
    {
        public SocketTextChannel Channel1 { get; set; }

        public SocketTextChannel Channel2 { get; set; }

        public DateTimeOffset LastMessage { get; set; } = DateTimeOffset.MinValue;
    }

    public class CrosstalkService
    {
        private readonly List<SocketTextChannel> _channels;

        public CrosstalkService()
        {
            _channels = new List<SocketTextChannel>();
            Calls = new List<CrosstalkCall>();
        }

        public List<CrosstalkCall> Calls { get; }

        public async Task AddChannelAsync(SocketTextChannel c)
        {
            if (Calls.Any(x => x.Channel1.Guild.Id == c.Guild.Id || x.Channel2.Guild.Id == c.Guild.Id))
            {
                await c.SendErrorAsync("Your guild is already in a crosstalk call!").ConfigureAwait(false);
                return;
            }

            if (_channels.Any(x => x.Guild.Id == c.Guild.Id))
            {
                await c.SendErrorAsync("Your guild is already ringing on the crosstalk phone!").ConfigureAwait(false);
                return;
            }

            if (_channels.FirstOrDefault() is SocketTextChannel receiverChannel)
            {
                Calls.Add(new CrosstalkCall
                {
                    Channel1 = c,
                    Channel2 = receiverChannel
                });
                _channels.Remove(receiverChannel);
                await c.EmbedAsync(new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription("You're connected on the crosstalk line! Say hi!")
                    .Build()).ConfigureAwait(false);
                await receiverChannel.EmbedAsync(new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription("You're connected on the crosstalk line! Say hi!")
                    .Build()).ConfigureAwait(false);

                _ = Task.Delay(TimeSpan.FromMinutes(1))
                    .ContinueWith(async t =>
                    {
                        await c.EmbedAsync(new EmbedBuilder()
                            .WithOkColor()
                            .WithDescription("Hanging up the crosstalk phone.")
                            .Build()).ConfigureAwait(false);
                        await receiverChannel.EmbedAsync(new EmbedBuilder()
                            .WithOkColor()
                            .WithDescription("Hanging up the crosstalk phone.")
                            .Build()).ConfigureAwait(false);
                        Calls.Remove(Calls.FirstOrDefault(x => x.Channel1.Id == c.Id || x.Channel2.Id == c.Id));
                    });

                return;
            }

            _channels.Add(c);
            _ = c.EmbedAsync(new EmbedBuilder()
                .WithOkColor()
                .WithDescription("Ringing on the crosstalk phone...")
                .Build())
                .ContinueWith(async _1 => await Task.Delay(TimeSpan.FromMinutes(1))
                .ContinueWith(async _2 =>
                {
                    if (Calls.All(x => x.Channel1.Id != c.Id && x.Channel2.Id != c.Id))
                    {
                        await c.SendErrorAsync("Hanging up the crosstalk phone because nobody answered.");
                        _channels.Remove(c);
                    }
                }));
        }
    }
}
