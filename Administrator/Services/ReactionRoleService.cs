using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Administrator.Extensions;
using Administrator.Services.Database;
using Administrator.Services.Database.Models;
using Discord;
using Discord.WebSocket;
using MoreLinq;
using NLog;

namespace Administrator.Services
{
    public class ReactionRoleService
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly DbService _db;

        public ReactionRoleService(BaseSocketClient client, DbService db)
        {
            _db = db;
            client.ReactionAdded += (m, c, r) =>
            {
                var _ = OnReactionAdded(m, c, r);
                return Task.CompletedTask;
            };
            client.ReactionRemoved += (m, c, r) =>
            {
                var _ = OnReactionRemoved(m, c, r);
                return Task.CompletedTask;
            };
        }

        public async Task AddListenerAsync(SocketUserMessage msg, IGuild guild, List<SocketRole> roles, List<IEmote> emotes)
        {
            var rrm = new ReactionRoleMessage
            {
                Id = (long) msg.Id,
                ChannelId = (long) msg.Channel.Id,
                EmoteStr = string.Join(' ', emotes.Select(x => x.ToString())),
                RoleStr = string.Join(',', roles.Select(x => x.Id)),
                GuildId = (long) guild.Id
            };

            await _db.InsertAsync(rrm).ConfigureAwait(false);

            try
            {
                foreach (var emote in emotes)
                {
                    await msg.AddReactionAsync(emote).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log.Warn(ex, ex.ToString);
            }
        }

        public async Task RemoveListenerAsync(SocketUserMessage msg)
        {
            var rrm = await _db.GetAsync<ReactionRoleMessage>(x => x.Id == (long) msg.Id).ConfigureAwait(false);

            if (rrm.FirstOrDefault() is ReactionRoleMessage r)
            {
                await _db.DeleteAsync(r).ConfigureAwait(false);

                try
                {
                    await msg.RemoveAllReactionsAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Warn(ex, ex.ToString);
                }
            }
        }

        private async Task OnReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel,
            SocketReaction reaction)
        {
            if (channel is IPrivateChannel || !(channel is SocketGuildChannel chnl)) return;

            var msg = await message.GetOrDownloadAsync().ConfigureAwait(false);
            if (msg is null) return;
            var rrms = await _db.GetAsync<ReactionRoleMessage>(x => x.Id == (long) msg.Id)
                .ConfigureAwait(false);

            if (!(reaction.User.IsSpecified
                  && reaction.User.Value is SocketGuildUser user
                  && !user.IsBot)) return;
            if (!(rrms.FirstOrDefault() is ReactionRoleMessage rrm)) return;

            var roles = chnl.Guild.Roles.Where(x => rrm.RoleIds.Contains(x.Id))
                .OrderBy(x => rrm.RoleIds.ToList().IndexOf(x.Id)).ToList();
            var emotes = rrm.Emotes.ToList();

            if (rrm.Emotes.FirstOrDefault(x => x.ToString().Equals(reaction.Emote.ToString()) || x.Name == reaction.Emote.Name) is IEmote e)
            {
                await user.AddRoleAsync(roles[emotes.IndexOf(e)]).ConfigureAwait(false);
            }
            else
            {
                await msg.RemoveReactionAsync(reaction.Emote, user).ConfigureAwait(false);
            }
        }

        private async Task OnReactionRemoved(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (channel is IPrivateChannel || !(channel is SocketGuildChannel chnl)) return;

            var msg = await message.GetOrDownloadAsync().ConfigureAwait(false);
            var rrms = await _db.GetAsync<ReactionRoleMessage>(x => x.Id == (long) msg.Id)
                .ConfigureAwait(false);

            if (!(reaction.User.IsSpecified && reaction.User.Value is SocketGuildUser user)) return;
            if (!(rrms.FirstOrDefault() is ReactionRoleMessage rrm)) return;

            var roles = chnl.Guild.Roles.Where(x => rrm.RoleIds.Contains(x.Id))
                .OrderBy(x => rrm.RoleIds.ToList().IndexOf(x.Id)).ToList();
            var emotes = rrm.Emotes.ToList();

            if (rrm.Emotes.FirstOrDefault(x => x.Equals(reaction.Emote)) is IEmote e)
            {
                await user.RemoveRoleAsync(roles[emotes.IndexOf(e)]).ConfigureAwait(false);
            }
        }
    }
}
