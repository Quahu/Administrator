using Administrator.Common;
using Administrator.Modules.Utility.Services;
using Administrator.Services;
using Administrator.Services.Database;
using Administrator.Services.Scheduler;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Administrator
{
    public static class Program
    {
        private static readonly Config Config = BotConfig.New();
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static CommandService commands;
        private static DbService db;
        private static SuggestionService suggestion;
        private static LoggingService logging;
        private static DiscordSocketClient client;
        private static IServiceProvider services;
        private static CommandHandler handler;
        private static SchedulerService scheduler;
        private static RandomService random;
        private static ReactionRoleService reaction;
        private static InteractiveService interactive;
        private static StatsService stats;

        public static async Task Main(string[] args)
        {
            client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Info,
                MessageCacheSize = 500,
                AlwaysDownloadUsers = true
            });

            commands = new CommandService();
            interactive = new InteractiveService(client);

            var crosstalk = new CrosstalkService(client);

            db = new DbService(Config.Db.FullLocation, client, commands);
            suggestion = new SuggestionService(client, db);
            logging = new LoggingService(client, db, commands, crosstalk);
            random = new RandomService();
            reaction = new ReactionRoleService(client, db);
            stats = new StatsService(client);
            services = new ServiceCollection()
                .AddSingleton(client)
                .AddSingleton(commands)
                .AddSingleton(db)
                .AddSingleton(suggestion)
                .AddSingleton(logging)
                .AddSingleton(random)
                .AddSingleton(interactive)
                .AddSingleton(crosstalk)
                .AddSingleton(stats)
                .AddSingleton(reaction)
                .BuildServiceProvider();
            handler = new CommandHandler(services);
            scheduler = new SchedulerService(db, client, crosstalk);

            await commands.AddModulesAsync(Assembly.GetEntryAssembly()).ConfigureAwait(false);
            await client.LoginAsync(TokenType.Bot, Config.BotToken).ConfigureAwait(false);
            await client.StartAsync().ConfigureAwait(false);
            await Task.Delay(-1);
        }
    }
}