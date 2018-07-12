using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Administrator.Common.Database;
using Administrator.Extensions;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Administrator.Common
{
    public abstract class AdminBase : AdminBase<SocketCommandContext>
    {
    }

    public class AdminBase<T> : ModuleBase<T> where T : SocketCommandContext
    {
        private readonly Stopwatch _watch = Stopwatch.StartNew();

        public AdminContext DbContext { get; set; }

        public CommandService Commands { get; set; }

        public Task<RuntimeResult> CommandError(string reason, string message = null,
            Embed embed = null)
            => Task.FromResult<RuntimeResult>(AdminResult.FromError(_watch.Elapsed, reason, message, embed));

        public Task<RuntimeResult> CommandSuccess(string message = null, Embed embed = null)
            => Task.FromResult<RuntimeResult>(AdminResult.FromSuccess(_watch.Elapsed, message, embed));

        public async Task<IUserMessage> EmbedAsync(Embed embed, TimeSpan? timeout = null)
            => await Context.Channel.EmbedAsync(embed, timeout);

        public async Task<IUserMessage> SendOkAsync(string message, TimeSpan? timeout = null)
            => await Context.Channel.SendOkAsync(message, timeout);

        public async Task<IUserMessage> SendErrorAsync(string message, TimeSpan? timeout = null)
            => await Context.Channel.SendErrorAsync(message, timeout);

        public async Task<IUserMessage> SendWarnAsync(string message, TimeSpan? timeout = null)
            => await Context.Channel.SendWarnAsync(message, timeout);

        public async Task<IUserMessage> SendWinAsync(string message, TimeSpan? timeout = null)
            => await Context.Channel.SendWinAsync(message, timeout);

        public async Task<IUserMessage> SendLoseAsync(string message, TimeSpan? timeout = null)
            => await Context.Channel.SendLoseAsync(message, timeout);

        public async Task AddCheckAsync()
            => await Context.Message.AddReactionAsync(new Emoji("\U00002705"));

        public async Task AddCrossAsync()
            => await Context.Message.AddReactionAsync(new Emoji("\U0000274c"));

        public async Task<SocketMessage> GetNextMessageAsync(NextMessageCriteria criteria, TimeSpan? timeout = null)
        {
            var eventTrigger = new TaskCompletionSource<SocketMessage>();

            Context.Client.MessageReceived += Handler;

            var trigger = eventTrigger.Task;
            var delay = Task.Delay(timeout ?? TimeSpan.FromSeconds(10));
            var task = await Task.WhenAny(trigger, delay);

            Context.Client.MessageReceived -= Handler;

            if (task == trigger)
                return await trigger;

            return null;

            Task Handler(SocketMessage message)
            {
                switch (criteria)
                {
                    case NextMessageCriteria.Guild:
                        if (message.Channel is SocketTextChannel ch
                            && ch.Guild.Id == Context.Guild.Id)
                        {
                            eventTrigger.SetResult(message);
                        }

                        break;
                    case NextMessageCriteria.Channel:
                        if (message.Channel.Id == Context.Channel.Id)
                        {
                            eventTrigger.SetResult(message);
                        }

                        break;
                    case NextMessageCriteria.User:
                        if (message.Author.Id == Context.User.Id)
                        {
                            eventTrigger.SetResult(message);
                        }

                        break;
                    case NextMessageCriteria.ChannelUser:
                        if (message.Author.Id == Context.User.Id
                            && message.Channel.Id == Context.Channel.Id)
                        {
                            eventTrigger.SetResult(message);
                        }

                        break;
                    case NextMessageCriteria.GuildUser:
                        if (message.Channel is SocketTextChannel c
                            && c.Guild.Id == Context.Guild.Id
                            && message.Author.Id == Context.User.Id)
                        {
                            eventTrigger.SetResult(message);
                        }

                        break;
                }

                return Task.CompletedTask;
            }
        }
    }
}
