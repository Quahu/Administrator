using Administrator.Common;
using Administrator.Common.Database;
using Administrator.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Imgur.API.Authentication.Impl;
using Microsoft.Extensions.DependencyInjection;
using MoreLinq;
using NLog;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace Administrator
{
    public class Administrator
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static async Task Main(string[] args)
        {
            Log.Info("Start.");
            BotConfig.Create();

            var services = new ServiceCollection()
                .AddDbContext<AdminContext>(ServiceLifetime.Scoped)
                .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
                {
                    AlwaysDownloadUsers = true,
                    MessageCacheSize = 500,
                    LogLevel = LogSeverity.Info
                }))
                .AddSingleton(new CommandService())
                .AddSingleton(new HttpClient())
                .AddSingleton(new ImgurClient(BotConfig.ImgurId, BotConfig.ImgurSecret))
                .BuildServiceProvider();

            var modules = (await services.GetRequiredService<CommandService>().AddModulesAsync(Assembly.GetEntryAssembly(), services)).ToList();
            Log.Info($"Registered {modules.Count} modules with {modules.SelectMany(x => x.Commands).DistinctBy(x => x.Name).Count()} commands.");

            await services.GetRequiredService<DiscordSocketClient>().LoginAsync(TokenType.Bot, BotConfig.Token);
            await services.GetRequiredService<DiscordSocketClient>().StartAsync();
            HandlerService.Initialize(services);

            Log.Info("Initialized.");

            await Task.Delay(-1);
        }
    }
}
