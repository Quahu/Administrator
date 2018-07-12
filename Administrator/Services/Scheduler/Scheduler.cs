using System;
using System.Linq;
using System.Threading.Tasks;
using Administrator.Common.Database;
using Administrator.Common.Database.Models;
using Administrator.Extensions;
using Discord;
using Discord.WebSocket;
using FluentScheduler;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace Administrator.Services.Scheduler
{
    public static class Scheduler
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static Registry _registry;

        public static void Initialize(IServiceProvider services)
        {
            _registry = new Registry();
            _registry.NonReentrantAsDefault();
            _registry.Schedule(async () => await RemoveExpiredMutesAsync(services))
                .NonReentrant().ToRunEvery(30).Seconds();
            JobManager.Initialize(_registry);
            Log.Info("Initialized.");
        }

        private static async Task RemoveExpiredMutesAsync(IServiceProvider services)
        {
            using (var scope = services.CreateScope())
            using (var ctx = scope.ServiceProvider.GetService<AdminContext>())
            {
                var client = services.GetService<DiscordSocketClient>();
                var mutes = ctx.Infractions.OfType<Mute>().Where(x => x.HasExpired && !x.HasBeenRevoked).ToList();

                foreach (var mute in mutes)
                {
                    if (!(client.GetGuild(mute.GuildId) is SocketGuild guild)
                        || !((guild.GetUser(mute.ReceieverId) ?? client.GetUser(mute.ReceieverId)) is SocketUser receiver))
                    {
                        ctx.Remove(mute);
                        continue;
                    }

                    mute.HasBeenRevoked = true;
                    mute.RevocationTimestamp = DateTimeOffset.UtcNow;
                    mute.RevokerId = client.CurrentUser.Id;
                    mute.RevokerName = client.CurrentUser.ToString();
                    ctx.Update(mute);

                    var gc = ctx.GetOrCreateGuildConfig(guild);

                    if (guild.GetTextChannel(gc.LogMuteChannelId) is SocketTextChannel logChannel)
                    {
                        await logChannel.EmbedAsync(new EmbedBuilder()
                            .WithWarnColor()
                            .WithTitle($"Mute - Case #{mute.Id}")
                            .WithDescription(
                                $"User **{receiver}** (`{receiver.Id}`) has been unmuted.")
                            .AddField("Reason", "Mute has expired.")
                            .WithFooter($"Moderator: {client.CurrentUser}", client.CurrentUser.GetAvatarUrl())
                            .WithTimestamp(mute.Timestamp)
                            .Build());
                    }

                    try
                    {
                        var dm = await receiver.GetOrCreateDMChannelAsync();
                        await dm.EmbedAsync(new EmbedBuilder()
                            .WithWarnColor()
                            .WithTitle($"Mute - Case #{mute.Id}")
                            .WithDescription(
                                $"You have been unmuted{(client.GetGuild(mute.GuildId) is SocketGuild g ? $" in {g.Name}" : string.Empty)}.")
                            .AddField("Reason", "Mute has expired.")
                            .WithFooter($"Moderator: {client.CurrentUser}", client.CurrentUser.GetAvatarUrl())
                            .WithTimestamp(mute.RevocationTimestamp)
                            .Build());
                    }
                    catch
                    {
                        // ignored
                    }

                    if (guild.GetRole(gc.MuteRoleId) is SocketRole muteRole
                        && guild.GetUser(mute.ReceieverId) is SocketGuildUser user)
                    {
                        await user.RemoveRoleAsync(muteRole);
                    }
                }

                ctx.SaveChanges();
            }
        }
    }
}
