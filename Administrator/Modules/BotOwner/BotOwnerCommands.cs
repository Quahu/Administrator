using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Administrator.Common;
using Administrator.Extensions;
using Administrator.Extensions.Attributes;
using Administrator.Services;
using Administrator.Services.Database;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Administrator.Modules.BotOwner
{
    [Name("BotOwner")]
    public class BotOwnerCommands : ModuleBase<SocketCommandContext>
    {
        private static readonly Config config = BotConfig.New();

        private readonly IReadOnlyList<string> serverRules = new List<string>
        {
            "1. Personal attacks or harassment towards other users will not be tolerated.",
            "2. Spamming or trolling in any of the text or voice channels will result in a mute.",
            "3. Do not post NSFW content of any kind. *\"If you have to ask, don't post it.\" - TheSpookiestUser*",
            "4. Please try to keep channels on-topic. This includes <#399408989684891651> - move any meaningful discussion to <#132349439506382848> or a relevant channel.",
            "5. Do not use this server as a place to appeal for subreddit bans. Please keep that to the sub.",
            "6. Do not impersonate anyone for any reason, \"famous\" or not.",
            "7. Don't try to bend the rules to your liking. We'll happily hand you a warning. Subsequent warnings will result in automated discipline by the Administrator.",
            "8. Don't advertise other Discord servers. If you want to share a link to another server, do it in DMs, as any links will be automatically purged.",
            "9. If there's reasonable suspicion you're causing significant harm to the TF2 community (including but not limited to: severe harassment, doxxing or raiding, and cheating of any kind), you may be punished without warning.",
            "∞. Follow the [**Discord Guidelines**](https://discordapp.com/guidelines). tl;dr - *use common sense.*"
        };

        private readonly IReadOnlyList<string> juniorModTeam = new List<string>
        {
            "<@!117478959599321092>",
            "<@!243076443045756929>",
            "<@!187061081745653760>",
            "<@!83107097465585664>",
            "<@!156085756643770368>",
            "<@!105093746944704512>",
            "<@!185970394287702016>",
            "<@!274297978800439297>"
        };

        private readonly IReadOnlyList<string> redditTeam = new List<string>
        {
            "<@!118863661333741568>",
            "<@!148926905490472962>",
            "<@!128478388842266624>",
            "<@!262578812179578880>",
            "<@!183961861388107786>",
            "<@!104775066327224320>",
            "<@!170765865883533312>",
            "<@!294567502187331584>",
            "<@!228112772192272384>",
            "<@!214418584259133442>",
            "<@!230479725980680192>",
            "<@!96783661721993216>",
            "<@!142693810776834048>"
        };

        private readonly IReadOnlyList<string> seniorModTeam = new List<string>
        {
            "<@!78372295906689024>",
            "<@!95697459690352640>",
            "<@!143294199087759361>",
            "<@!167452465317281793>"
        };

        [Command("tag")]
        [Summary("Send or update a tag in a given channel.")]
        [Usage("{p}tag 1234567890 suggestion", "{p}tag -u 1234567890 0987654321 suggestion")]
        [RequireContext(ContextType.Guild)]
        [RequireOwner]
        private async Task OwnerTagAsync(params string[] args)
        {
            var updateMode = false;
            List<string> remainder;
            var eb = new EmbedBuilder();
            var text = string.Empty;

            for (var i = 0; i < args.Length; i++) args[i] = args[i].ToLower();

            if (args[0].Equals("-u"))
            {
                updateMode = true;
                remainder = args.TakeLast(args.Length - 1).ToList();
            }
            else
            {
                remainder = args.ToList();
            }

            var channelId = (ulong) 0;
            var messageId = (ulong) 0;
            string type;

            if (updateMode)
            {
                messageId = ulong.Parse(remainder[0]);
                channelId = ulong.Parse(remainder[1]);
                type = remainder[2];
            }
            else
            {
                channelId = ulong.TryParse(remainder[0], out var result) ? result : Context.Channel.Id;
                type = channelId == Context.Channel.Id ? remainder[0] : remainder[1];
            }

            if (type.Equals("suggestion"))
            {
                var description = $"To use `{config.BotPrefix}suggest` properly, the command usage is as follows:\n"
                                  + $"```css\n{config.BotPrefix}suggest [suggestion]\n```\n"
                                  + "If you wish to include an image with your suggestion, post it as a direct link via a site like imgur. It will automatically be included with the suggestion.\n"
                                  + "\nScrewed up your suggestion? Delete it by reacting with :wastebasket: (\\:wastebasket\\:)!";

                eb.WithOkColor()
                    .WithTitle($"\"How 2 {config.BotPrefix}suggest plz?\"")
                    .WithDescription(description);
            }
            else if (type.Equals("teams"))
            {
                eb.WithOkColor()
                    .AddField("Subreddit Moderator Team:", string.Join(' ', redditTeam))
                    .AddField("Senior Community Moderator Team:", string.Join(' ', seniorModTeam))
                    .AddField("Junior Community Moderator Team:", string.Join(' ', juniorModTeam));
            }
            else if (type.Equals("roles1"))
            {
                eb.WithOkColor()
                    .WithTitle("Click on an emoji to subscribe to certain types of news!")
                    .WithDescription($"{Emote.Parse("<a:TFSpin:445033252936351755>")} - Only major TF2 updates.\n" +
                                     $"{Emote.Parse("<:TFLogo:445024255055495170>")} - All TF2 updates, even the updated localization files.\n" +
                                     $"{Emote.Parse("<:TFSnoo:445039284869791744>")} - Subreddit updates.\n" +
                                     $"{Emote.Parse("<:TFDiscord:445038772858388480>")} - Discord server updates.\n" +
                                     $"{Emote.Parse("<:TFadmin:445055026507808769>")} - Bot changelogs and releases.")
                    .WithFooter("Your role will be mentioned only when news of that type rolls out.");
            }
            else if (type.Equals("roles2"))
            {
                eb.WithOkColor()
                    .WithTitle("Click on an emoji to choose a region!")
                    .WithDescription($"{Emote.Parse("<:TFNorthAmerica:445043954078056458>")} - North America\n" +
                                     $"{Emote.Parse("<:TFSouthAmerica:445043620492345354>")} - South America\n" +
                                     $"{Emote.Parse("<:TFEurope:445044264284585985>")} - Europe\n" +
                                     $"{Emote.Parse("<:TFAsia:445044329518596116>")} - Asia\n" +
                                     $"{Emote.Parse("<:TFOceania:445043872557432842>")} - Oceania\n" +
                                     $"{Emote.Parse("<:TFAfrica:445049041429069835>")} - Africa\n");
            }
            else if (type.Equals("roles3"))
            {
                eb.WithOkColor()
                    .WithTitle("Pick a team!");
            }
            else if (type.Equals("rules"))
            {
                var commMods = Context.Guild.Roles.First(x => x.Name == "Community Mod");
                eb.WithOkColor()
                    .WithTitle("Welcome to the official /r/tf2 Discord server!")
                    .WithDescription("```diff\n- SERVER RULES -\n```\n" +
                                     string.Join("\n", serverRules))
                    .AddField("Need immediate assistance?",
                        $"Get the community moderators' attention with {commMods.Mention} or try contacting any online server staff.")
                    .AddField("Want to use the bot?", $"Utilize the `{config.BotPrefix}help` command to get DMed a list of all commands.\nThe bot's prefix for all commands is `{config.BotPrefix}`.")
                    .AddField("Want to decorate your profile with some fancy roles?", "Head on over to <#443454347477778442> to give yourself some! You can customize your notifications, region, classes, and even choose a team!");
            }
            else if (type.Equals("servers"))
            {
                eb.WithOkColor()
                    .WithTitle("/r/tf2 Community Server list (subject to change)")
                    .WithDescription("▬▬▬▬▬▬▬▬▬▬")
                    .AddField("/r/tf2 Community | NA West | Vanilla",
                        "steam://connect/na1.tf2.game")
                    .AddField("/r/tf2 Community | EU | Vanilla",
                        "steam://connect/eu1.tf2.game")
                    .AddField("/r/tf2 Community | OCE | Vanilla",
                        "steam://connect/oce1.tf2.game")
                    .AddField("r/tf2 (un)official server!",
                        "steam://connect/rtf2.game.nfoservers.com:27015");

                text = "https://discord.gg/zxrBZuK";
            }
            else if (type.Equals("roles"))
            {
                eb.WithOkColor()
                    .WithTitle(
                        "Add a reaction for the class role you want!")
                    .WithDescription(
                        "If you want to remove a role, remove the reaction for it.\nIf you aren't given a role, try removing and re-adding the reaction.");
            }
            else
            {
                return;
            }

            var chnl = Context.Guild.GetChannel(channelId);

            if (messageId == 0)
            {
                await (chnl as ISocketMessageChannel).SendMessageAsync(text, embed: eb.Build()).ConfigureAwait(false);
            }
            else
            {
                var msg = await (chnl as IMessageChannel).GetMessageAsync(messageId).ConfigureAwait(false);
                await (msg as IUserMessage).ModifyAsync(x =>
                    { 
                        x.Embed = eb.Build();
                        x.Content = text;
                    }).ConfigureAwait(false);
            }
        }

        [Command("setgame")]
        [Summary("Change the bot's playing status.")]
        [Usage("{p}setgame Playing Team Fortress 2")]
        [RequireOwner]
        private async Task ModifyGameAsync(params string[] args)
        {
            // ActivityType.Playing, Listening, Watching, Streaming
            if (Enum.TryParse(args[0], out ActivityType type))
            {
                var remainder = string.Join(" ", args.TakeLast(args.Length - 1));
                await Context.Client.SetGameAsync(remainder, type: type).ConfigureAwait(false);
                await Context.Message.AddReactionAsync(new Emoji("\U00002705")).ConfigureAwait(false);
            }
            else
            {
                await Context.Message.AddReactionAsync(new Emoji("\U0000274c")).ConfigureAwait(false);
            }
        }

        [Command("roleid")]
        [Alias("rid")]
        [RequireOwner]
        private async Task GetRoleIdsAsync([Remainder] string roleName)
        {
            var eb = new EmbedBuilder();
            var roles = Context.Guild.Roles.Where(x =>
                x.Name.Equals(roleName, StringComparison.InvariantCultureIgnoreCase))
                .OrderByDescending(x => x.Position)
                .ToList();
            if (roles.Any())
            {
                eb.WithOkColor()
                    .WithDescription(string.Join("\n\n", roles.Select(x => $"{x.Name} - `{x.Id}`")));
            }
            else
            {
                eb.WithErrorColor()
                    .WithDescription("No roles found by that name.");
            }

            await Context.Channel.EmbedAsync(eb.Build()).ConfigureAwait(false);
        }
    }
}