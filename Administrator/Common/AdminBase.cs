using System;
using System.Threading.Tasks;
using Administrator.Extensions;
using Administrator.Services.Database;
using Discord;
using Discord.Commands;
using Discord.Rest;

namespace Administrator.Common
{
    public class AdminBase : AdminBase<SocketCommandContext>
    {
    }

    public class AdminBase<T> : ModuleBase<T>
        where T : SocketCommandContext
    {
        public DbService Db { get; set; }

        public async Task<RestUserMessage> EmbedAsync(Embed embed, TimeSpan? timeout = null)
        {
            var m = await Context.Channel.SendMessageAsync(string.Empty, embed: embed).ConfigureAwait(false);

            if (timeout is TimeSpan ts)
            {
                await Task.Delay(ts).ConfigureAwait(false);

                try
                {
                    await m.DeleteAsync().ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }

            return m;
        }

        public async Task<RestUserMessage> SendConfirmAsync(string message, TimeSpan? timeout = null)
        {
            var m = await Context.Channel.SendMessageAsync(string.Empty, embed: new EmbedBuilder()
                .WithOkColor()
                .WithDescription(message)
                .Build()).ConfigureAwait(false);

            if (timeout is TimeSpan ts)
            {
                await Task.Delay(ts).ConfigureAwait(false);

                try
                {
                    await m.DeleteAsync().ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }

            return m;
        }

        public async Task<RestUserMessage> SendErrorAsync(string message, TimeSpan? timeout = null)
        {
            var m = await Context.Channel.SendMessageAsync(string.Empty, embed: new EmbedBuilder()
                .WithErrorColor()
                .WithDescription(message)
                .Build()).ConfigureAwait(false);

            if (timeout is TimeSpan ts)
            {
                await Task.Delay(ts).ConfigureAwait(false);

                try
                {
                    await m.DeleteAsync().ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }

            return m;
        }
    }
}
