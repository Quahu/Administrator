using System;
using System.Linq;
using System.Threading.Tasks;
using Administrator.Extensions;
using Administrator.Services.Database;
using Administrator.Services.Database.Models;
using Discord;
using Discord.WebSocket;

namespace Administrator.Modules.Utility.Services
{
    public class SuggestionService
    {
        private static readonly IEmote delet_this = new Emoji("\U0001f5d1");
        private readonly DiscordSocketClient client;
        private readonly DbService db;

        public SuggestionService(DiscordSocketClient cl, DbService db)
        {
            client = cl;
            this.db = db;
            client.ReactionAdded += HandleReactionAsync;
        }

        public async Task AddNewAsync(IUserMessage message, SocketGuildUser author)
        {
            var guild = await db.GetOrCreateGuildConfigAsync((message.Channel as SocketGuildChannel).Guild)
                .ConfigureAwait(false);

            await message.AddReactionAsync(DiscordExtensions.GetEmote(guild.UpvoteArrow)).ConfigureAwait(false);
            await message.AddReactionAsync(DiscordExtensions.GetEmote(guild.DownvoteArrow)).ConfigureAwait(false);

            var suggestion = new Suggestion
            {
                MessageId = (long) message.Id,
                UserId = (long) author.Id,
                Content = message.Embeds.FirstOrDefault()?.Description,
                GuildId = (long) author.Guild.Id,
                ImageUrl = message.Embeds.FirstOrDefault()?.Image.GetValueOrDefault().Url ?? string.Empty
            };

            await db.InsertAsync(suggestion).ConfigureAwait(false);
        }

        private async Task HandleReactionAsync(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel,
            SocketReaction reaction)
        {
            if (channel is IPrivateChannel) return;
            if (reaction.UserId == client.CurrentUser.Id) return;
            var msg = await message.GetOrDownloadAsync().ConfigureAwait(false);

            var gc = await db.GetOrCreateGuildConfigAsync((channel as SocketGuildChannel).Guild).ConfigureAwait(false);
            var suggestions = await db.GetAsync<Suggestion>().ConfigureAwait(false);
            var emote = reaction.Emote;

            if (suggestions.FirstOrDefault(x => x.MessageId == (long) msg.Id) is Suggestion s)
            {
                if (reaction.Emote.Equals(DiscordExtensions.GetEmote(gc.UpvoteArrow)) || reaction.Emote.Equals(DiscordExtensions.GetEmote(gc.DownvoteArrow)))
                {
                    var upvotes = msg.Reactions.FirstOrDefault(x => x.Key.Equals(DiscordExtensions.GetEmote(gc.UpvoteArrow))).Value
                                      .ReactionCount - 1;
                    var downvotes = msg.Reactions.FirstOrDefault(x => x.Key.Equals(DiscordExtensions.GetEmote(gc.DownvoteArrow))).Value
                                        .ReactionCount - 1;
                    // -1 to account for the bot's own vote

                    s.Upvotes = upvotes;
                    s.Downvotes = downvotes;

                    await db.UpdateAsync(s).ConfigureAwait(false);
                }
                else if (reaction.Emote.Equals(delet_this) && (s.UserId == (long) msg.Author.Id
                         || reaction.User.IsSpecified && (reaction.User.Value as SocketGuildUser).Roles.Any(x => x.Id == (ulong) gc.PermRole)))
                {
                    await db.DeleteAsync(s).ConfigureAwait(false);
                    await msg.DeleteAsync().ConfigureAwait(false);
                }
                else
                {
                    if (reaction.User.IsSpecified) await msg.RemoveReactionAsync(reaction.Emote, reaction.User.Value).ConfigureAwait(false);
                }
            }
        }

        public async Task RemoveAsync(IUserMessage message)
        {
            var suggestions =
                await db.GetAsync<Suggestion>(x => x.MessageId == (long) message.Id).ConfigureAwait(false);
            if (suggestions.FirstOrDefault() is Suggestion s) await db.DeleteAsync(s).ConfigureAwait(false);
        }
    }
}