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
        private static readonly Logger log = LogManager.GetCurrentClassLogger();

        public SchedulerService(DbService db, DiscordSocketClient client)
        {
            var muteChecker = new MuteChecker(db, client);
            var ltpChecker = new LtpChecker(db, client);
            
            log.Info("Adding scheduler(s) to registry.");
            Schedule(async () => await muteChecker.CheckMutesAsync().ConfigureAwait(false))
                .NonReentrant().ToRunEvery(1).Minutes();
            Schedule(async () => await ltpChecker.RemoveExpiredLtpPlayersAsync().ConfigureAwait(false))
                .NonReentrant().ToRunEvery(1).Minutes();

            client.Ready += () =>
            {
                JobManager.Initialize(this);
                log.Info("Starting schedules.");
                return Task.CompletedTask;
            };

            client.Disconnected += ex =>
            {
                JobManager.Stop();
                log.Warn("Job manager stopped. Client disconnected.");
                log.Warn(ex, ex.ToString);
                return Task.CompletedTask;
            };
        }
    }
}
