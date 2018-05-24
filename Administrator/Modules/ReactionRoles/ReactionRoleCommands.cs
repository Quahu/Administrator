using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Administrator.Extensions;
using Administrator.Extensions.Attributes;
using Administrator.Services;
using Administrator.Services.Database;
using Administrator.Services.Database.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Administrator.Modules.ReactionRoles
{
    [Name("ReactionRoles")]
    [RequireContext(ContextType.Guild)]
    [RequirePermRole]
    public class ReactionRoleCommands : ModuleBase<SocketCommandContext>
    {
        private readonly DbService _db;
        private readonly ReactionRoleService _reaction;

        public ReactionRoleCommands(DbService db, ReactionRoleService reaction)
        {
            _db = db;
            _reaction = reaction;
        }

        [Command("addreactionrole", RunMode = RunMode.Async)]
        [Alias("arr")]
        [Summary("Add a reaction role listener to a message.\n" +
                 "The format is like so: `#channel messageId emote1 role1 emote2 role2 ... emoteN roleN`")]
        [Usage("{p}arr #somechannel 1234567890 :emote1: \"Role 1\" :emote2: \"Role 2\" :emote3: \"Role 3\"")]
        [Remarks("Limit 10 listeners per guild.")]
        private async Task AddReactionRoleMessageAsync(IMessageChannel channel, params string[] args)
        {
            var rrms = await _db.GetAsync<ReactionRoleMessage>(x => x.GuildId == (long) Context.Guild.Id).ConfigureAwait(false);
            if (rrms.Count >= 10)
            {
                await Context.Channel
                    .SendErrorAsync("Maximum number of listeners already in use. Remove old or unused listeners first.")
                    .ConfigureAwait(false);
            }
            if (!ulong.TryParse(args[0], out var messageId))
            {
                await Context.Channel.SendErrorAsync("First argument was not a valid message ID.")
                    .ConfigureAwait(false);
                return;
            }

            var input = args.Skip(1).ToList();

            if (input.Count % 2 != 0)
            {
                await Context.Channel
                    .SendErrorAsync(
                        "Input did not have an even number of arguments. Make sure any multiple-word roles are \"enclosed in quotes\".")
                    .ConfigureAwait(false);
                return;
            }

            var emotes = new List<IEmote>();
            var roles = new List<SocketRole>();

            for (var i = 0; i < input.Count; i++)
            {
                if (i % 2 == 0)
                {
                    emotes.Add(Emote.TryParse(input[i], out var result) ? result : (IEmote) new Emoji(input[i]));
                }
                else
                {
                    roles.Add(Context.Guild.Roles.FirstOrDefault(x => x.Name.Equals(input[i], StringComparison.InvariantCultureIgnoreCase)));
                }
            }

            if (roles.Any(x => x is null))
            {
                await Context.Channel
                    .SendErrorAsync("One or more of the roles supplied is not valid. Check inputs and try again.")
                    .ConfigureAwait(false);
                return;
            }

            SocketUserMessage msg = null;

            try
            {
                var m = await channel.GetMessageAsync(messageId).ConfigureAwait(false);
                if (m is SocketUserMessage n)
                {
                    msg = n;
                }
            }
            catch
            {
                // ignore
            }

            if (msg is null)
            {
                await Context.Channel.SendErrorAsync("Could not find a message to listen on in that channel. It may not be cached, try sending a new message and using that one.").ConfigureAwait(false);
                return;
            }

            if (rrms.Any(x => x.Id == (long) messageId))
            {
                await Context.Channel
                    .SendErrorAsync(
                        "A reaction role listener already exists for that message. Remove the old listener before continuing.")
                    .ConfigureAwait(false);
                return;
            }

            var desc = string.Empty;

            for (var i = 0; i < emotes.Count; i++)
            {
                desc += $"{emotes[i]} => {roles[i].Name}\n";
            }

            var edit = await Context.Channel.SendConfirmAsync("Building reaction role listener. Please wait.");
            await _reaction.AddListenerAsync(msg, Context.Guild, roles, emotes).ConfigureAwait(false);

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle($"Successfully added a reaction role listener! ID: {msg.Id}")
                .WithDescription(desc)
                .WithFooter("If any of the above looks wrong, remove the listener, check inputs, and try again.");

            await edit.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
        }

        [Command("removereactionrole")]
        [Alias("rrr")]
        [Summary("Remove a reaction role listener given a valid message ID.")]
        [Usage("{p}rrr 1234567890")]
        private async Task RemoveReactionRoleMessageAsync(long id)
        {
            var rrm = await _db.GetAsync<ReactionRoleMessage>(x => x.GuildId == (long) Context.Guild.Id && x.Id == id).ConfigureAwait(false);

            if (rrm.FirstOrDefault() is ReactionRoleMessage r)
            {
                var eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription($"Reaction role listener for message ID {r.Id} removed.");
                await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
                await _db.DeleteAsync(r).ConfigureAwait(false);
            }
            else
            {
                await Context.Channel.SendErrorAsync("Could not find a reaction role listener by that message ID.")
                    .ConfigureAwait(false);
            }
        }

        [Command("getreactionroles")]
        [Alias("grr")]
        [Summary("Displays reaction role listeners on the current guild.")]
        [Usage("{p}grr")]
        private async Task GetReactionRoleMessagesAsync()
        {
            var rrms = await _db.GetAsync<ReactionRoleMessage>(x => x.GuildId == (long) Context.Guild.Id)
                .ConfigureAwait(false);

            if (rrms.Any())
            {
                var eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle($"Reaction role messages for {Context.Guild.Name}")
                    .WithDescription(string.Join("\n\n",
                        rrms.Select(x =>
                            $"{x.Id} => {Context.Guild.GetChannel((ulong) x.ChannelId)} - {x.RoleIds.Count()} roles/emotes")));
                await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
            }
            else
            {
                await Context.Channel.SendErrorAsync("No reaction roles found on this guild.").ConfigureAwait(false);
            }
        }
    }
}
