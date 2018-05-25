using Administrator.Common;
using Administrator.Extensions;
using Administrator.Extensions.Attributes;
using Administrator.Services.Database;
using Administrator.Services.Database.Models;
using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Administrator.Modules.Phrases
{
    [Name("Phrase")]
    [RequireContext(ContextType.Guild)]
    [RequirePermissionsPass]
    public class PhraseCommands : ModuleBase<SocketCommandContext>
    {
        private static readonly Config Config = BotConfig.New();
        private static bool wasCreated;
        private static List<ulong> isRemoving;
        private readonly DbService db;

        public PhraseCommands(DbService db)
        {
            this.db = db;

            if (wasCreated) return;

            isRemoving = new List<ulong>();
            wasCreated = true;
        }

        [Command("addphrase")]
        [Alias("setphrase")]
        [Summary("Create your own personal phrase. Messages sent in this guild containing this phrase will increment its counter.")]
        [Remarks("Phrase must not be a phrase already in use by another user.")]
        [Usage("{p}addphrase nice meme")]
        private async Task AddUserPhraseAsync([Remainder] string phrase)
        {
            if (isRemoving.Any(x => x == Context.User.Id))
            {
                await Context.Channel
                    .SendErrorAsync("You are currently in the middle of removing your phrase. Please wait.")
                    .ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrWhiteSpace(phrase)) return;
            phrase = phrase.ToLower();

            var userPhrases = await db.GetAsync<UserPhrase>(x => x.GuildId == (long) Context.Guild.Id).ConfigureAwait(false);
            var blacklist = await db.GetAsync<BlacklistedPhrase>(x => x.GuildId == (long) Context.Guild.Id).ConfigureAwait(false);
            var eb = new EmbedBuilder();
            var gc = await db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            if (userPhrases.Any(up => up.UserId == (long) Context.User.Id))
            {
                eb.WithErrorColor()
                    .WithDescription(
                        "You already have a phrase! Please remove your old phrase before creating a new one.")
                    .WithFooter($"Use {Config.BotPrefix}removephrase ({Config.BotPrefix}remphrase) to remove your phrase.");
            }
            else if (blacklist.Any(x => x.PhraseStr.Equals(phrase, StringComparison.InvariantCultureIgnoreCase)
                        || phrase.ToLower().Contains(x.PhraseStr) && x.Match == PhraseBlacklistMatch.Containing))
            {
                eb.WithErrorColor()
                    .WithDescription("You cannot create a phrase that is blacklisted or contains a blacklisted word.");
            }
            else if (userPhrases.FirstOrDefault(up => up.Phrase.Equals(phrase)) is UserPhrase existingUserPhrase)
            {
                eb.WithErrorColor()
                    .WithDescription(
                        $"{Context.Guild.GetUser((ulong) existingUserPhrase.UserId).ToString() ?? "Someone"} already has that phrase!");
            }
            else if (phrase.Length < gc.PhraseMinLength)
            {
                eb.WithErrorColor()
                    .WithDescription($"New phrases must be at least {gc.PhraseMinLength} characters long.");
            }
            else if (await db.InsertAsync(new UserPhrase
                {
                    GuildId = (long) Context.Guild.Id,
                    UserId = (long) Context.User.Id,
                    Phrase = phrase
                }) > 0)
            {
                eb.WithOkColor()
                    .WithTitle("New phrase added!")
                    .WithDescription(
                        $"Any occurrence of \"{phrase}\" in a message in this guild will increase its counter.");
            }
            else
            {
                eb.WithErrorColor()
                    .WithDescription("Some error occured. Please report this.");
            }

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);

        }


        [Command("removephrase", RunMode = RunMode.Async)]
        [Alias("remphrase")]
        [Summary("Remove your set phrase.")]
        [Usage("{p}remphrase")]
        [Remarks("Moderators can remove others phrases by mentioning them after the command.")]
        [Priority(1)]
        private async Task RemoveUserPhraseAsync()
            => await RemoveUserPhraseAsync(Context.User).ConfigureAwait(false);

        [Command("removephrase", RunMode = RunMode.Async)]
        [Alias("remphrase")]
        [Summary("Remove your set phrase.")]
        [Usage("{p}remphrase")]
        [Remarks("Moderators can remove others phrases by mentioning them after the command.")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [Priority(0)]
        private async Task RemoveUserPhraseAsync([Remainder] IUser target)
        {
            var userPhrases = await db.GetAsync<UserPhrase>(x => x.GuildId == (long) Context.Guild.Id && x.UserId == (long) target.Id).ConfigureAwait(false);
            var eb = new EmbedBuilder();

            if (!(userPhrases.FirstOrDefault() is UserPhrase up))
            {
                eb.WithErrorColor()
                    .WithDescription("That user has not set a phrase.");

                await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
                return;
            }

            eb.WithOkColor()
                .WithDescription("Removing all instances of this phrase. Please wait.");
            isRemoving.Add(target.Id);
            var msg = await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);

            var ebComplete = new EmbedBuilder();
            await db.DeleteAsync(up).ConfigureAwait(false);
            userPhrases = await db.GetAsync<UserPhrase>(x => x.GuildId == (long) Context.Guild.Id && x.UserId == (long) target.Id).ConfigureAwait(false);

            if (userPhrases.Any())
            {
                ebComplete.WithErrorColor()
                    .WithDescription("Could not remove that user's phrase. Please report this.");
            }
            else
            {
                ebComplete.WithOkColor()
                    .WithDescription("Phrase successfully removed.");
            }

            isRemoving.Remove(target.Id);

            await msg.ModifyAsync(x => x.Embed = ebComplete.Build()).ConfigureAwait(false);
        }

        [Command("phraseblacklist")]
        [Alias("pbl")]
        [Summary("Add a specific phrase to the guild's phrase blacklist.\n" +
                 "Supply the `-c` flag to prevent any phrases containing the supplied text from being created.\n" +
                 "Supply no arguments to view the current blacklist for this guild.")]
        [Usage("{p}pbl -c the", "{p}pbl poop")]
        [RequirePermRole]
        [Priority(1)]
        private async Task BlacklistPhraseAsync()
        {
            var blacklist = await db.GetAsync<BlacklistedPhrase>(x => x.GuildId == (long) Context.Guild.Id)
                .ConfigureAwait(false);
            blacklist = blacklist.OrderByDescending(x => x.Id).ToList();
            var eb = new EmbedBuilder()
                .WithOkColor();
            if (blacklist.Any())
            {
                eb.WithTitle($"Phrase blacklist for {Context.Guild}")
                    .WithDescription(string.Join(", ",
                        blacklist.Select(x => $"**{x.Id}**: `{x.PhraseStr}` ({x.Match})")));
            }
            else
            {
                eb.WithDescription("No phrases blacklisted on this guild.");
            }

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        [Command("phraseblacklist")]
        [Alias("pbl")]
        [Summary("Add a specific phrase to the guild's phrase blacklist.\n" +
                 "Supply the `-c` flag to prevent any phrases containing the supplied text from being created.\n" +
                 "Supply no arguments to view the current blacklist for this guild.")]
        [Usage("{p}pbl -c the", "{p}pbl fuck")]
        [RequirePermRole]
        [Priority(0)]
        private async Task BlacklistPhraseAsync(params string[] input)
        {
            if (input is null || input.Length < 1) return;
            string phrase;
            var eb = new EmbedBuilder()
                .WithOkColor();
            var match = PhraseBlacklistMatch.Exact;
            if (input[0] == "-c" && input.Length > 1)
            {
                phrase = string.Join(' ', input.Skip(1)).ToLower();
                eb.WithDescription($"Blacklisted any phrase containing `{phrase}`.");
                match = PhraseBlacklistMatch.Containing;
            }
            else
            {
                phrase = string.Join(' ', input).ToLower();
                eb.WithDescription($"Blacklisted phrase `{phrase}`.");
            }

            var bl = new BlacklistedPhrase
            {
                PhraseStr = phrase,
                GuildId = (long) Context.Guild.Id,
                Type = (long) match
            };

            await db.InsertAsync(bl).ConfigureAwait(false);
            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        [Command("removephraseblacklist")]
        [Alias("rpbl")]
        [Summary("Remove a phrase blacklist entry by ID.")]
        [Usage("{p}rpbl 5")]
        [RequirePermRole]
        private async Task RemoveBlacklistedPhraseAsync(long id)
        {
            var blacklist = await db.GetAsync<BlacklistedPhrase>(x => x.GuildId == (long) Context.Guild.Id);
            if (blacklist.FirstOrDefault(x => x.Id == id) is BlacklistedPhrase bl)
            {
                var eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle($"Blacklisted phrase with ID {id} removed.")
                    .WithDescription($"**{bl.Id}**: `{bl.PhraseStr}` ({bl.Match})");
                await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
                await db.DeleteAsync(bl).ConfigureAwait(false);
            }
            else
            {
                await Context.Channel.SendErrorAsync("No blacklisted phrase with that ID found.").ConfigureAwait(false);
            }
        }

        [Command("phrasestats")]
        [Alias("phrase")]
        [Summary("Show stats for a user's phrase.")]
        [Usage("{p}phrase", "{p}phrase @SomeOtherUser")]
        [Remarks("Supply a target to see that user's phrase. Defaults to yourself.")]
        private async Task GetPhraseAsync([Remainder]IUser target = null)
        {
            target = target ?? Context.Message.Author;
            var phrases = await db.GetAsync<Phrase>(x => x.GuildId == (long) Context.Guild.Id).ConfigureAwait(false);
            var userPhrases = await db.GetAsync<UserPhrase>(x => x.GuildId == (long) Context.Guild.Id).ConfigureAwait(false);
            var eb = new EmbedBuilder();

            if (userPhrases.All(x => x.UserId != (long) target.Id))
            {
                eb.WithErrorColor()
                    .WithDescription(target.Id == Context.Message.Author.Id
                    ? "You don't have a phrase set!"
                    : "This user doesn't have a phrase set!")
                    .WithFooter($"Use {Config.BotPrefix}addphrase to add a phrase.");
                await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
                return;
            }

            if (!(userPhrases.FirstOrDefault(x => x.UserId == (long) target.Id) is UserPhrase userPhrase)) return;

            var count = phrases.Where(x => x.UserPhraseId == userPhrase.Id).ToList().Count;

            if (count == 0)
            {
                eb.WithOkColor()
                    .WithTitle($"Stats for {target}'s phrase:")
                    .WithDescription($"\"{userPhrase.Phrase}\"")
                    .AddField("Occurrences:",
                        count, true)
                    .AddField("Top channels:", "N/A", true)
                    .AddField("Top users:", "N/A", true)
                    .AddField("Last heard:", "N/A", true);

                await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
                return;
            }

            var order = phrases.GroupBy(x => x.UserPhraseId)
                .OrderByDescending(x => x.Count())
                .ToList();

            var bestChannelIds = phrases.Where(x => x.UserPhraseId == userPhrase.Id)
                .GroupBy(x => x.ChannelId)
                .OrderByDescending(y => y.Count())
                .Take(5)
                .Select(x => x.First().ChannelId)
                .ToList();
            var bestChannels = Context.Guild.Channels.Where(c => bestChannelIds.Contains((long) c.Id))
                .OrderBy(x => bestChannelIds.IndexOf((long) x.Id))
                .ToList();
            var channelCounts = phrases
                .Where(x => x.UserPhraseId == userPhrase.Id && bestChannelIds.Contains(x.ChannelId))
                .GroupBy(x => x.ChannelId)
                .OrderBy(x => bestChannelIds.IndexOf(x.First().ChannelId))
                .Select(x => x.Count())
                .ToList();
            var bestUserIds = phrases.Where(x => x.UserPhraseId == userPhrase.Id)
                .GroupBy(x => x.UserId)
                .OrderByDescending(y => y.Count())
                .Take(5)
                .Select(x => x.First().UserId)
                .ToList();
            var bestUsers = Context.Guild.Users.Where(u => bestUserIds.Contains((long) u.Id))
                .OrderBy(x => bestUserIds.IndexOf((long) x.Id))
                .ToList();
            var userCounts = phrases.Where(x => x.UserPhraseId == userPhrase.Id && bestUserIds.Contains(x.UserId))
                .GroupBy(x => x.UserId)
                .OrderBy(x => bestUserIds.IndexOf(x.First().UserId))
                .Select(x => x.Count())
                .ToList();

            var last = phrases.Where(x => x.UserPhraseId == userPhrase.Id)
                .OrderByDescending(x => x.Timestamp)
                .FirstOrDefault()
                .Timestamp;

            var topChannels = string.Empty;
            for (var i = 0; i < bestChannels.Count; i++)
            {
                topChannels += $"#{bestChannels[i].Name} - {channelCounts[i]}\n";
            }

            var topUsers = string.Empty;
            for (var i = 0; i < bestUsers.Count; i++)
            {
                topUsers += $"{bestUsers[i]} - {userCounts[i]}\n";
            }

            eb.WithOkColor()
                .WithTitle($"Stats for {target}'s phrase:")
                .WithDescription($"\"{userPhrase.Phrase}\"")
                .AddField("Occurrences:", count,
                    true)
                .AddField("Top channels:", topChannels, true)
                .AddField("Top users:", topUsers, true)
                .AddField("Last heard:", $"{last:g} GMT", true)
                .WithFooter(
                    $"This user's phrase is ranked #{order.FindIndex(x => x.First().UserPhraseId == userPhrase.Id) + 1}/{order.Count}.");

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        [Command("phrasetop")]
        [Summary("Show the top 10 phrases of length `y`. Supply no length to not filter by length.\n" +
                 "You may also supply a `-e` or `-m` flag to display only emotes or mentions.")]
        [Usage("{p}phrasetop", "{p}phrasetop 5")]
        [Priority(1)]
        private async Task PhraseTopAsync(int length = 0)
        {
            var userPhrases = await db.GetAsync<UserPhrase>(x => x.GuildId == (long) Context.Guild.Id).ConfigureAwait(false);
            var phrases = await db.GetAsync<Phrase>(x => x.GuildId == (long) Context.Guild.Id).ConfigureAwait(false);

            if (length > 0)
            {
                userPhrases = userPhrases.Where(x => x.Phrase.Length == length).ToList();
                phrases = phrases.Where(x => userPhrases.Select(y => y.Id).Contains(x.UserPhraseId)).ToList();
            }

            var groups = phrases.GroupBy(p => p.UserPhraseId)
                .OrderByDescending(g => g.Count()).ToList();

            var num = groups.Count < 10 ? groups.Count : 10;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle($"Top {num} phrases{(length > 0 ? $" (length {length})" : string.Empty)}:");

            var description = string.Empty;
            for (var i = 0; i < num; i++)
            {
                if (groups[i].FirstOrDefault() is Phrase p
                    && userPhrases.FirstOrDefault(x => x.Id == p.UserPhraseId) is UserPhrase up)
                {
                    description +=
                        $"\"{up.Phrase}\" - owned by {Context.Guild.GetUser((ulong) up.UserId)} - Score: {phrases.Count(x => x.UserPhraseId == up.Id)}\n";
                }
                //var p = groups.Skip(i).First().First();
                //var up = userPhrases.First(x => x.Id == p.UserPhraseId);
                //var count = phrases.Count(x => x.UserPhraseId == up.Id);
            }

            eb.WithDescription(description);

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        [Command("phrasetop")]
        private async Task PhraseTopAsync(string filter)
        {

            var userPhrases = await db.GetAsync<UserPhrase>(x => x.GuildId == (long) Context.Guild.Id).ConfigureAwait(false);
            var phrases = await db.GetAsync<Phrase>(x => x.GuildId == (long) Context.Guild.Id).ConfigureAwait(false);
            var eb = new EmbedBuilder()
                .WithOkColor();

            switch (filter)
            {
                case "-m":
                    userPhrases = userPhrases.Where(x => Regex.IsMatch(x.Phrase, @"[^<>]")).ToList();
                    eb.WithTitle($"Top {(userPhrases.Count < 10 ? userPhrases.Count : 10)} phrases with mentions:");
                    break;
                case "-e":
                    userPhrases = userPhrases.Where(x => Regex.IsMatch(x.Phrase, @"\p{Cs}") || Emote.TryParse(x.Phrase, out _)).ToList();
                    eb.WithTitle($"Top {(userPhrases.Count < 10 ? userPhrases.Count : 10)} emote phrases:");
                    break;
            }

            phrases = phrases.Where(x => userPhrases.Select(y => y.Id).Contains(x.UserPhraseId)).ToList();


            var groups = phrases.GroupBy(p => p.UserPhraseId)
                .OrderByDescending(g => g.Count()).ToList();

            var description = string.Empty;
            for (var i = 0; i < (userPhrases.Count < 10 ? userPhrases.Count : 10); i++)
            {
                if (groups[i].FirstOrDefault() is Phrase p
                    && userPhrases.FirstOrDefault(x => x.Id == p.UserPhraseId) is UserPhrase up)
                {
                    description +=
                        $"**{i + 1}**. \"{up.Phrase}\" - owned by {Context.Guild.GetUser((ulong) up.UserId)} - Score: {phrases.Count(x => x.UserPhraseId == up.Id)}\n";
                }
                //var p = groups.Skip(i).First().First();
                //var up = userPhrases.First(x => x.Id == p.UserPhraseId);
                //var count = phrases.Count(x => x.UserPhraseId == up.Id);
            }

            eb.WithDescription(description);

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        [Command("searchphrase")]
        [Alias("sphrase")]
        [Summary("Search for phrases using a given keyword.")]
        [Usage("{p}sphrase nice meme")]
        private async Task SearchPhraseAsync([Remainder] string query)
        {
            var eb = new EmbedBuilder()
                .WithOkColor();
            var userPhrases = await db.GetAsync<UserPhrase>(x => x.GuildId == (long) Context.Guild.Id).ConfigureAwait(false);
            var phraseStrs = userPhrases.Select(x => x.Phrase).ToList();

            var bestMatches = StringExtensions.GetBestMatchesFor(phraseStrs, query, 5);
            var bestPhrases = userPhrases.Where(x => bestMatches.Contains(x.Phrase))
                .OrderBy(x => bestMatches.IndexOf(x.Phrase))
                .ToList();

            var field = string.Empty;

            var phrases = await db.GetAsync<Phrase>(x => x.GuildId == (long) Context.Guild.Id).ConfigureAwait(false);

            foreach (var p in bestPhrases)
            {
                var count = phrases.Where(x => x.UserPhraseId == p.Id).ToList().Count();
                field += $"\"{p.Phrase}\" - owned by {Context.Guild.GetUser((ulong) p.UserId)} - Score: {count}\n";
            }

            eb.WithFooter($"You searched for: \"{query}\"")
                .AddField("Closest 5 matches:", string.Join("\n", field));

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }
    }
}