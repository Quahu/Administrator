using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Administrator.Services.Database;
using Administrator.Services.Scheduler.Schedules;
using Discord.WebSocket;
using FluentScheduler;
using NLog;

namespace Administrator.Services.Scheduler
{
    public class SchedulerService : Registry
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public SchedulerService(DbService db, DiscordSocketClient client)
        {
            var muteChecker = new MuteChecker(db, client);
            var ltpChecker = new LtpChecker(db, client);
            
            Log.Info("Adding scheduler(s) to registry.");
            Schedule(async () => await muteChecker.CheckMutesAsync().ConfigureAwait(false))
                .NonReentrant().ToRunEvery(1).Minutes();
            Schedule(async () => await ltpChecker.RemoveExpiredLtpPlayersAsync().ConfigureAwait(false))
                .NonReentrant().ToRunEvery(1).Minutes();

            client.Connected += () =>
            {
                JobManager.Initialize(this);
                Log.Info("Client connected. Starting schedules.");
                return Task.CompletedTask;
            };

            client.Disconnected += ex =>
            {
                JobManager.Stop();
                Log.Warn("Client disconnected. Job manager stopped.");
                Log.Warn(ex, ex.ToString);
                return Task.CompletedTask;
            };
        }
    }
}
