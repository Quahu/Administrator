using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Administrator.Extensions;
using Administrator.Extensions.Attributes;
using Administrator.Services;
using Administrator.Services.Database;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using MoreLinq;

namespace Administrator.Modules.Fun
{
    internal enum EightBallOutcome
    {
        Positive,
        Uncertain,
        Negative
    }

    [Name("Fun")]
    public class FunCommands : ModuleBase<SocketCommandContext>
    {
        private static readonly Config Config = BotConfig.New();
        private readonly DbService _db;
        private readonly RandomService _random;
        private readonly LoggingService _logging;
        private readonly CrosstalkService _crosstalk;

        private readonly IReadOnlyDictionary<string, EightBallOutcome> eightBallResponses = new Dictionary<string, EightBallOutcome>
        {
            {"It is certain", EightBallOutcome.Positive},
            {"It is decidedly so", EightBallOutcome.Positive},
            {"Without a doubt", EightBallOutcome.Positive},
            {"Yes, definitely", EightBallOutcome.Positive},
            {"You may rely on it", EightBallOutcome.Positive},
            {"As I see it, yes", EightBallOutcome.Positive},
            {"Most likely", EightBallOutcome.Positive},
            {"Outlook good", EightBallOutcome.Positive},
            {"Yes", EightBallOutcome.Positive},
            {"Signs point to yes", EightBallOutcome.Positive},
            {"Reply hazy - try again", EightBallOutcome.Uncertain},
            {"Ask again later", EightBallOutcome.Uncertain},
            {"Better not tell you now", EightBallOutcome.Uncertain},
            {"Cannot predict now", EightBallOutcome.Uncertain},
            {"Concentrate and ask again", EightBallOutcome.Uncertain},
            {"Don't count on it", EightBallOutcome.Negative},
            {"My reply is no", EightBallOutcome.Negative},
            {"My sources say no", EightBallOutcome.Negative},
            {"Outlook not so good", EightBallOutcome.Negative},
            {"Very doubtful", EightBallOutcome.Negative}
        };

        public FunCommands(DbService db, RandomService random, LoggingService logging, CrosstalkService crosstalk)
        {
            _db = db;
            _random = random;
            _logging = logging;
            _crosstalk = crosstalk;
        }

        [Command("crosstalk", RunMode = RunMode.Async)]
        [Summary("Start or join a crosstalk call.\n" +
                 "If no calls are found, starts an empty call and waits for another channel to connect.\n" +
                 "You may supply the `-c` flag to generate a code that you can use to connect to your call directly.\n" +
                 "Using a code is optional, omitting a code for the command will simply find the first available call.\n" +
                 "You may supply a given code to automatically connect to that specific call. See **Usage** for examples.")]
        [Usage("{p}crosstalk", "{p}crosstalk -c", "{p}crosstalk 1b5dft61")]
        [RequirePermissionsPass]
        [RequireContext(ContextType.Guild)]
        private async Task StartCrosstalkSessionAsync(string codeOrFlag = null)
        {
            if (!(Context.Channel is SocketTextChannel c)) return;

            if (_crosstalk.Calls.FirstOrDefault(x => x.IsConnected && x.ContainsChannel(c)) is CrosstalkCall
                call)
            {
                _crosstalk.Calls.Remove(call);

                if (c.Id == call.Channel1.Id)
                {
                    await call.Channel2.SendConfirmAsync("The other caller hung up the crosstalk phone.").ConfigureAwait(false);
                    await call.Channel1.SendConfirmAsync("You hung up the crosstalk phone.").ConfigureAwait(false);
                    return;
                }

                await call.Channel1.SendConfirmAsync("The other caller hung up the crosstalk phone.").ConfigureAwait(false);
                await call.Channel2.SendConfirmAsync("You hung up the crosstalk phone.").ConfigureAwait(false);

                var msgs1 = await (call.Channel1 as IMessageChannel).GetMessagesAsync(20, CacheMode.CacheOnly)
                    .FlattenAsync().ConfigureAwait(false);
                var msgs2 = await (call.Channel2 as IMessageChannel).GetMessagesAsync(20, CacheMode.CacheOnly)
                    .FlattenAsync().ConfigureAwait(false);

                var m1 = msgs1.Where(x =>
                    x.Content.Contains("** is typing...") && x.Author.Id == Context.Client.CurrentUser.Id).ToList();
                var m2 = msgs2.Where(x =>
                    x.Content.Contains("** is typing...") && x.Author.Id == Context.Client.CurrentUser.Id).ToList();

                if (m1.Any())
                {
                    foreach (var m in m1)
                    {
                        try
                        {
                            await m.DeleteAsync().ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }

                if (m2.Any())
                {
                    foreach (var m in m2)
                    {
                        try
                        {
                            await m.DeleteAsync().ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }

                return;
            }

            if (!string.IsNullOrWhiteSpace(codeOrFlag))
            {
                if (codeOrFlag.Equals("-c", StringComparison.InvariantCultureIgnoreCase))
                {
                    await _crosstalk.AddChannelAsync(c, true).ConfigureAwait(false);
                    return;
                }

                if (_crosstalk.Calls.All(x => !x.ConnectionCode.Equals(codeOrFlag)))
                {
                    await c.SendErrorAsync("No calls found with that code.").ConfigureAwait(false);
                    return;
                }
            }

            await _crosstalk.AddChannelAsync(c, false, codeOrFlag).ConfigureAwait(false);
        }

        //[Ratelimit(1, 2, Measure.Minutes)]
        [Command("vote", RunMode = RunMode.Async)]
        [Summary("Start a vote in the current channel. Attach an image or image link to have the bot display the image in the embed.")]
        [Usage("{p}vote Ban all mods.")]
        [Remarks("Stats for votes are currently not tracked.")]
        [RequirePermissionsPass]
        [RequireContext(ContextType.Guild)]
        private async Task StartVoteAsync([Remainder] string input)
        {
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithAuthor(new EmbedAuthorBuilder
                {
                    IconUrl = Context.Message.Author.AvatarUrl(),
                    Name = $"{Context.Message.Author} wants to call a vote:"
                });

            if (Context.Message.Attachments.Any())
            {
                var attachment = Context.Message.Attachments.First();
                if (attachment.Url.ToLower().EndsWith("png", "jpg", "bmp", "jpeg")) eb.WithImageUrl(attachment.Url);
                eb.WithDescription(input);
            }
            else if (input.TryExtractUri(out var updated, out var uri))
            {
                eb.WithImageUrl(uri.ToString())
                    .WithDescription(updated);
            }
            else
            {
                eb.WithDescription(input);
            }

            _logging.AddIgnoredMessages(new List<IMessage> {Context.Message});
            try
            {
                if (Context.Message.MentionedUsers.Count < 1)
                {
                    await Context.Message.DeleteAsync().ConfigureAwait(false);
                }
            }
            catch
            {
                // ignored
            }
            var msg = await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
            var guildConfig = await _db.GetOrCreateGuildConfigAsync(Context.Guild).ConfigureAwait(false);

            await msg.AddReactionAsync(DiscordExtensions.GetEmote(guildConfig.UpvoteArrow)).ConfigureAwait(false);
            await msg.AddReactionAsync(DiscordExtensions.GetEmote(guildConfig.DownvoteArrow)).ConfigureAwait(false);
        }

        //[Ratelimit(1, 0.25, Measure.Minutes)]
        [Command("roll")]
        [Summary("Roll some dice.")]
        [Usage("{p}roll 1d20 + 5 + 1d6")]
        [RequirePermissionsPass]
        private async Task RollDiceAsync([Remainder] string input = "1d10")
        {
            var split = input.Split('+').ToList();
            var eb = new EmbedBuilder()
                .WithWinColor();

            var total = 0;
            foreach (var section in split)
            {
                if (section.Trim().Split('d').ToList().Count == 2)
                {
                    var temp = section.Split('d');
                    if (!uint.TryParse(temp[0], out var numDice) || !uint.TryParse(temp[1], out var numSides)) continue;
                    for (var i = 0; i < numDice; i++)
                    {
                        if (numSides == 0)
                        {
                            total += 0;
                        }
                        else total += _random.Next(1, numSides);
                    }
                }
                else if (uint.TryParse(section.Trim(), out var num))
                {
                    total += (int) num;
                }
            }

            eb.WithTitle($"Input: {input}");
            eb.WithDescription(total == 0 ? "Rolled 0. Idiot." : $"Rolled {total}.");

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }

        //[Ratelimit(1, 1, Measure.Minutes)]
        [Command("big")]
        [Summary("Blow up an emote.")]
        [Usage("{p}big :MedGrin:")]
        [RequirePermissionsPass]
        private async Task BigEmoteAsync(string emoji)
        {
            var eb = new EmbedBuilder();
            if (Emote.TryParse(emoji, out var result))
            {
                eb.WithOkColor()
                    .WithImageUrl(result.Url);
                await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
            }
        }

        [Command("choose")]
        [Summary("Choose from a list of things, separated by `;`.")]
        [Usage("{p}choose eat;sleep")]
        [RequirePermissionsPass]
        private async Task ChooseAsync([Remainder] string toChoose)
        {
            var options = toChoose.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

            if (options.Count < 2)
            {
                await Context.Channel.SendErrorAsync("You must select more than one thing!");
                return;
            }

            await Context.Channel.EmbedAsync(new EmbedBuilder()
                .WithOkColor()
                .WithDescription($":thinking: I choose...**{options[_random.Next(0, (uint) options.Count - 1)]}**!")
                .Build()).ConfigureAwait(false);
        }

        [Command("rate")]
        [Summary("Rate something out of 10.")]
        [Usage("{p}rate having a gf")]
        [RequirePermissionsPass]
        private async Task GetRatingAsync([Remainder] string toRate)
        {
            var r = GetRating(toRate);
            var eb = new EmbedBuilder()
                .WithDescription($"{Context.User.Mention}, I'd rate **{toRate}** a {GetRating(toRate)}/10.");

            if (r > 5)
            {
                eb.WithWinColor();
            }
            else if (r == 5)
            {
                eb.WithWarnColor();
            }
            else if (r < 5)
            {
                eb.WithLoseColor();
            }

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);

            ulong GetRating(string s)
            {
                s = s.ToLower();
                var md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
                var bytes = Encoding.UTF8.GetBytes(s[0].ToString()).ToList();
                foreach (var t in s)
                {
                    bytes = bytes.Concat(Encoding.UTF8.GetBytes(s[0].ToString())).ToList();
                }
                bytes = md5.ComputeHash(bytes.ToArray()).ToList();
                var sb = new StringBuilder();
                foreach (var b in bytes)
                {
                    sb.Append(b.ToString("x2").ToLower());
                }

                var ratingStr = sb.ToString();

                var rating = (ulong) 0;

                for (var i = 0; i < ratingStr.Length; i += 2)
                {
                    rating += Convert.ToUInt64((ratingStr[i] + ratingStr[i + 1]).ToString(), 16);
                }

                return rating % 11;
            }
        }

        [Command("8ball")]
        [Alias("8")]
        [Summary("Ask the Magic 8-ball a question.")]
        [Usage("{p}8ball will I die tomorrow?")]
        [RequirePermissionsPass]
        private async Task Ask8BallAsync([Remainder] string question)
        {
            if (string.IsNullOrWhiteSpace(question)) return;
            var response = eightBallResponses.Keys.ToList()[_random.Next(0, (uint) eightBallResponses.Keys.Count() - 1)];

            if (Context.Message.MentionedUsers.Any())
            {
                foreach (var m in Context.Message.MentionedUsers)
                {
                    question = question.Replace("<@!", "<@")
                        .Replace("<@", "<@!")
                        .Replace(m.Mention, m.ToString());
                }
            }

            var eb = new EmbedBuilder()
                .WithAuthor(new EmbedAuthorBuilder
                {
                    IconUrl = "https://i.imgur.com/wHs7DQN.png",
                    Name = question
                })
                .WithDescription($"{response}, {Context.User.Mention}.");

            switch (eightBallResponses[response])
            {
                case EightBallOutcome.Positive:
                    eb.WithWinColor();
                    break;
                case EightBallOutcome.Uncertain:
                    eb.WithWarnColor();
                    break;
                case EightBallOutcome.Negative:
                    eb.WithLoseColor();
                    break;
            }

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }
    }
}