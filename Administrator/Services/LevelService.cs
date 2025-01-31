﻿using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Administrator.Commands;
using Administrator.Common;
using Administrator.Database;
using Administrator.Extensions;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using SixLabors.Shapes;
using Image = SixLabors.ImageSharp.Image;

namespace Administrator.Services
{
    public sealed class LevelService : IService
    {
        public static readonly TimeSpan XpGainInterval = TimeSpan.FromMinutes(5);
        public const int XP_RATE = 50;
        private const int MINIMUM_MESSAGE_LENGTH = 20;

        private readonly LocalizationService _localization;
        private readonly LoggingService _logging;
        private readonly DiscordSocketClient _client;
        private readonly ConfigurationService _config;
        private readonly HttpClient _http;
        private readonly IServiceProvider _provider;

        public LevelService(LocalizationService localization, LoggingService logging, DiscordSocketClient client,
            ConfigurationService config, HttpClient http, IServiceProvider provider)
        {
            _localization = localization;
            _logging = logging;
            _client = client;
            _config = config;
            _http = http;
            _provider = provider;
        }

        public async Task<Stream> CreateXpImageAsync(AdminCommandContext context, IUser target)
        {
            var output = new MemoryStream();

            try
            {
                var user = await context.Database.GetOrCreateGlobalUserAsync(target.Id);
                var globalUsers = await context.Database.GlobalUsers.OrderByDescending(x => x.TotalXp).ToListAsync();

                GuildUser guildUser = null;
                Guild guild = null;
                Stream guildIcon = null;
                var guildPosition = 1;
                if (target is IGuildUser guildTarget)
                {
                    guildUser = await context.Database.GetOrCreateGuildUserAsync(target.Id, guildTarget.GuildId);
                    guild = await context.Database.GetOrCreateGuildAsync(guildTarget.GuildId);
                    guildIcon = await _http.GetStreamAsync(guildTarget.Guild.IconUrl);

                    var guildUsers = await context.Database.GuildUsers.OrderByDescending(x => x.TotalXp).ToListAsync();
                    guildPosition = guildUsers.IndexOf(guildUser) + 1;
                }

                var guildOffset = guild?.Settings.HasFlag(GuildSettings.XpTracking) == true
                    ? 45
                    : 0;

                using var background = Image.Load(new FileStream("./Data/Images/01.png", FileMode.Open));
                using var avatar = Image.Load(await _http.GetStreamAsync(target.GetAvatarOrDefault()));
                using var canvas = new Image<Rgba32>(Configuration.Default, 450, 300);

                canvas.Mutate(cnvs =>
                {
                    var sb = new StringBuilder();
                    foreach (var c in target.Username)
                    {
                        if (char.IsLetterOrDigit(c)) sb.Append(c);
                    }

                    sb.Append($"#{target.Discriminator}");

                    var userFontSize = 20;
                    var box = TextMeasurer.MeasureBounds(sb.ToString(),
                        new RendererOptions(ImageTools.Fonts.TF2(userFontSize)));
                    while (box.Width > 420 || box.Height > 20)
                    {
                        box = TextMeasurer.MeasureBounds(sb.ToString(),
                            new RendererOptions(ImageTools.Fonts.TF2(--userFontSize)));
                    }

                    // Draw XP image (background)
                    cnvs.DrawImage(background, PixelColorBlendingMode.Normal, 1);
                    var circle = new EllipsePolygon(new PointF(100, 100), 10);
                    cnvs.Fill(GraphicsOptions.Default, Rgba32.AliceBlue, circle);

                    // Draw outer bounding box
                    cnvs.FillPolygon(ImageTools.Colors.DarkButTransparent,
                        new PointF(10, 190 - guildOffset),
                        new PointF(440, 190 - guildOffset),
                        /*
                    new PointF(440, 210 - guildReduction),
                    new PointF(10, 210 - guildReduction),
                    new PointF(440, 210 - guildReduction),
                    */
                        new PointF(440, 290 - guildOffset),
                        new PointF(10, 290 - guildOffset));

                    // Draw avatar bounding box
                    cnvs.FillPolygon(ImageTools.Colors.Blurple,
                        new PointF(385, 215 - guildOffset),
                        new PointF(435, 215 - guildOffset),
                        new PointF(435, 265 - guildOffset),
                        new PointF(385, 265 - guildOffset));

                    // Draw avatar
                    cnvs.DrawImage(avatar.Clone(x => x.Resize(50, 50)),
                        new Point(385, 215 - guildOffset), PixelColorBlendingMode.Normal,
                        PixelAlphaCompositionMode.SrcOver, 1);
                    /*
                    cnvs.DrawImage(avatar.Clone(x => x.Resize(50, 50)),
                        PixelColorBlendingMode.Normal, 1,
                        new Point(385, 215 - guildReduction));
                    */

                    // Draw avatar bounding box outline
                    cnvs.DrawPolygon(Rgba32.WhiteSmoke, 2,
                        new PointF(386, 216 - guildOffset),
                        new PointF(434, 216 - guildOffset),
                        new PointF(434, 264 - guildOffset),
                        new PointF(386, 264 - guildOffset));

                    // Write username
                    cnvs.DrawText(sb.ToString().Trim(), ImageTools.Fonts.TF2(userFontSize),
                        Rgba32.WhiteSmoke, new PointF(15, 195 - guildOffset));

                    /* Write user info
                    if (!string.IsNullOrWhiteSpace(global.Info))
                    {
                        cnvs.DrawText(new TextGraphicsOptions { WrapTextWidth = 350 },
                            global.Info,
                            ImageTools.Fonts.TF2Secondary(13),
                            Rgba32.WhiteSmoke,
                            new PointF(15, 215 - guildOffset));
                    }*/
                    
                    // Draw inner box (XP bar outline)
                    cnvs.FillPolygon(ImageTools.Colors.LessDark,
                        new PointF(75, 272 - guildOffset),
                        new PointF(435, 272 - guildOffset),
                        new PointF(435, 285 - guildOffset),
                        new PointF(75, 285 - guildOffset));

                    // Draw current XP bar
                    cnvs.FillPolygon(ImageTools.Colors.XpBar,
                        new PointF(77, 274 - guildOffset),
                        new PointF(356F * ((float) user.CurrentLevelXp / user.NextLevelXp) + 77,
                            274 - guildOffset),
                        new PointF(356F * ((float)user.CurrentLevelXp / user.NextLevelXp) + 77,
                            283 - guildOffset),
                        new PointF(77, 283 - guildOffset));

                    // Write current level text
                    cnvs.DrawText(new TextGraphicsOptions {HorizontalAlignment = HorizontalAlignment.Center},
                        $"Tier {user.Tier}, Level {user.Level} ({user.Grade} Grade)",
                        ImageTools.Fonts.TF2(13),
                        ImageTools.Colors.GetGradeColor(user.Grade),
                        new PointF(255, 259 - guildOffset));

                    // Write current XP text
                    cnvs.DrawText(new TextGraphicsOptions {HorizontalAlignment = HorizontalAlignment.Center},
                        $"{user.TotalXp} / {user.NextLevelTotalXp} XP",
                        ImageTools.Fonts.TF2(13),
                        Rgba32.WhiteSmoke,
                        new PointF(255, 273 - guildOffset));


                    /* Draw current level
                    cnvs.DrawImage(
                        currentLevel.Clone(x => x.Resize(45 / currentLevel.Height * currentLevel.Width, 45)),
                        PixelColorBlendingMode.Normal, 1,
                        new Point(45, 285 - guildOffset), Justification.BottomCenter); */

                    // Write current global position
                    cnvs.DrawText(new TextGraphicsOptions {HorizontalAlignment = HorizontalAlignment.Center},
                        $"Global position #{globalUsers.IndexOf(user) + 1}",
                        ImageTools.Fonts.TF2(11),
                        Rgba32.WhiteSmoke,
                        new PointF(255, 248 - guildOffset));

                    if (guildUser is {} && guildOffset > 0)
                    {
                        // Draw guild bounding box
                        // 270
                        cnvs.FillPolygon(ImageTools.Colors.DarkButTransparent,
                            new PointF(10, 250),
                            new PointF(440, 250),
                            new PointF(440, 295),
                            new PointF(10, 295));

                        // Draw guild XP bar outline
                        cnvs.FillPolygon(ImageTools.Colors.LessDark,
                            new PointF(75, 277),
                            new PointF(435, 277),
                            new PointF(435, 290),
                            new PointF(75, 290));

                        // Draw guild XP bar
                        cnvs.FillPolygon(ImageTools.Colors.XpBar,
                            new PointF(77, 279),
                            new PointF(356F * ((float) guildUser.CurrentLevelXp / guildUser.NextLevelXp) + 77,
                                279),
                            new PointF(356F * ((float) guildUser.CurrentLevelXp / guildUser.NextLevelXp) + 77,
                                288),
                            new PointF(77, 288));

                        // Write current guild level text
                        cnvs.DrawText(new TextGraphicsOptions {HorizontalAlignment = HorizontalAlignment.Center},
                            $"Tier {guildUser.Tier}, Level {guildUser.Level} ({guildUser.Grade} Grade)",
                            ImageTools.Fonts.TF2(13),
                            ImageTools.Colors.GetGradeColor(guildUser.Grade),
                            new PointF(255, 264));

                        // Write current guild XP text
                        cnvs.DrawText(new TextGraphicsOptions {HorizontalAlignment = HorizontalAlignment.Center},
                            $"{guildUser.TotalXp} / {guildUser.NextLevelTotalXp} XP",
                            ImageTools.Fonts.TF2(13),
                            Rgba32.WhiteSmoke,
                            new PointF(255, 278));

                        // using (var guildLevel = Image.Load(guildLevelStream))
                        using (var guildImage = Image.Load(guildIcon))
                        {
                            /* Draw current guild level
                            cnvs.DrawImage(guildLevel.Clone(x => x.Resize(40 / guildLevel.Height * guildLevel.Width, 40)),
                                PixelColorBlendingMode.Normal, 1,
                                new Point(45, 292),
                                Justification.BottomCenter); */

                            // Draw current guild icon
                            guildImage.Mutate(img => img.Resize(18, 18));
                            cnvs.DrawImage(guildImage,
                                ImageTools.Justify(new Point(435, 255), guildImage, Justification.TopRight),
                                PixelColorBlendingMode.Normal, PixelAlphaCompositionMode.Dest, 1f);
                        }
                        
                        // Write current guild position
                        cnvs.DrawText(new TextGraphicsOptions {HorizontalAlignment = HorizontalAlignment.Center},
                            $"Guild position #{guildPosition}",
                            ImageTools.Fonts.TF2(11),
                            Rgba32.WhiteSmoke,
                            new PointF(255, 253));
                    }
                });

                canvas.SaveAsPng(output);
                output.Seek(0, SeekOrigin.Begin);
            }
            catch (Exception ex)
            {
                await _logging.LogErrorAsync(ex, "Profiles");
            }

            return output;
        }

        public async ValueTask IncrementXpAsync(SocketUserMessage message)
        {
            if (message.Source != MessageSource.User ||
                message.Resolve()?.Length < MINIMUM_MESSAGE_LENGTH ||
                !(message.Channel is SocketTextChannel channel)) return;

            using var ctx = new AdminDatabaseContext(_provider);

            var now = DateTimeOffset.UtcNow;
            var guild = await ctx.GetOrCreateGuildAsync(channel.Guild.Id);
            if (!guild.Settings.HasFlag(GuildSettings.XpTracking)) return;

            var user = await ctx.GetOrCreateGlobalUserAsync(message.Author.Id);
            
            if (now - user.LastXpGain > XpGainInterval)
            {
                var currentLevel = user.Level;
                user.TotalXp += XP_RATE;
                user.LastXpGain = now;
                ctx.GlobalUsers.Update(user);

                if (user.Level > currentLevel)
                {
                    // TODO: Discard necessary?
                    _ = NotifyOnLevelUpAsync(message, guild, user, user.LevelUpPreferences);
                }
            }

            var guildUser = await ctx.GetOrCreateGuildUserAsync(message.Author.Id, guild.Id);
            if (now - guildUser.LastXpGain > guild.XpGainInterval)
            {
                var currentLevel = guildUser.Level;
                guildUser.TotalXp += guild.XpRate;
                guildUser.LastXpGain = now;
                ctx.GuildUsers.Update(guildUser);

                if (guildUser.Level > currentLevel)
                {
                    // TODO: Discard necessary?
                    _ = NotifyOnLevelUpAsync(message, guild, guildUser, user.LevelUpPreferences);
                }
            }

            await ctx.SaveChangesAsync();
        }

        public IEmote GetLevelEmote(User user)
        {
            if (_config.EmoteServerIds.Count == 0)
                throw new ArgumentException("No emote servers could be found to process this.", nameof(user));

            var emote = _config.EmoteServerIds.Select(x => _client.GetGuild(x)).SelectMany(x => x.Emotes)
                .FirstOrDefault(x => x.Name.Equals($"tier_{user.Tier}_level_{user.Level}"));

            return emote ?? EmoteTools.Level; // TODO: Log if null?
        }

        private async Task NotifyOnLevelUpAsync(SocketMessage message, Guild guild, User user, LevelUpNotification preferences)
        {
            if (preferences == LevelUpNotification.None) return;
            var levelEmote = GetLevelEmote(user);
            var type = user is GlobalUser
                ? "global"
                : "guild";
            
            if (preferences.HasFlag(LevelUpNotification.Channel) &&
                guild.LevelUpWhitelist.HasFlag(LevelUpNotification.Channel))
            {
                await message.Channel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithSuccessColor()
                    .WithThumbnailUrl(EmoteTools.GetUrl(GetLevelEmote(user)))
                    .WithAuthor(_localization.Localize(guild.Language, $"user_levelup_{type}"),
                        message.Author.GetAvatarOrDefault())
                    .WithDescription(_localization.Localize(guild.Language, "user_levelup", message.Author.Mention,
                        user.Tier, user.Level)).Build());
            }

            if (preferences.HasFlag(LevelUpNotification.DM))
            {
                using var ctx = new AdminDatabaseContext();
                var language = (user as GlobalUser)?.Language ??
                               (await ctx.GetOrCreateGlobalUserAsync(user.Id)).Language;

                _ = message.Author.SendMessageAsync(embed: new EmbedBuilder()
                    .WithSuccessColor()
                    .WithThumbnailUrl(EmoteTools.GetUrl(GetLevelEmote(user)))
                    .WithAuthor(_localization.Localize(language, $"user_levelup_{type}"),
                        message.Author.GetAvatarOrDefault())
                    .WithDescription(_localization.Localize(language, "user_levelup", message.Author.Mention, user.Tier,
                        user.Level)).Build());
            }

            if (preferences.HasFlag(LevelUpNotification.Reaction) &&
                guild.LevelUpWhitelist.HasFlag(LevelUpNotification.Reaction))
            {
                var levelUpEmote = guild.LevelUpEmote ?? EmoteTools.LevelUp;
                await message.AddReactionAsync(levelUpEmote);
                await message.AddReactionAsync(levelEmote);
            }
        }

        Task IService.InitializeAsync()
            => _logging.LogInfoAsync("Initialized.", "Profiles");
    }
}