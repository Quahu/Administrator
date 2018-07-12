using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Administrator.Common;
using Administrator.Common.Database;
using Administrator.Extensions;
using Administrator.Services;
using Administrator.Services.Scheduler;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Imgur.API.Authentication.Impl;
using Microsoft.EntityFrameworkCore;
using MoreLinq;
using Nett;
using NLog;

namespace Administrator
{
    public static class Administrator
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static DiscordSocketClient _client;
        private static CommandService _commands;
        private static IServiceProvider _services;

        public static async Task Main(string[] args)
        {
            Log.Info("Start.");
            BotConfig.Create();

            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                AlwaysDownloadUsers = true,
                MessageCacheSize = 500,
                LogLevel = LogSeverity.Info
            });
            _commands = new CommandService();
            _services = new ServiceCollection()
                .AddDbContext<AdminContext>(ServiceLifetime.Scoped)
                .AddSingleton(_client)
                .AddSingleton(_commands)
                .AddSingleton(new HttpClient())
                .AddSingleton(new ImgurClient(BotConfig.ImgurId, BotConfig.ImgurSecret))
                .BuildServiceProvider();

            var modules = (await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services)).ToList();
            Log.Info($"Registered {modules.Count} modules with {modules.SelectMany(x => x.Commands).DistinctBy(x => x.Name).Count()} commands.");

            await _client.LoginAsync(TokenType.Bot, BotConfig.Token);
            await _client.StartAsync();
            _client.Ready += OnReady;
            Log.Info("Initialized.");

            await Task.Delay(-1);
        }

        private static Task OnReady()
        {
            LoggingService.Initialize(_services);
            CommandHandler.Initialize(_services);
            Scheduler.Initialize(_services);
            StatsService.Initialize(_services);
            _client.MessageReceived += CommandHandler.HandleAsync;
            return Task.CompletedTask;
        }
    }
}
