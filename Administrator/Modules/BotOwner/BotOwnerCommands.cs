using Administrator.Common;
using Administrator.Extensions;
using Administrator.Extensions.Attributes;
using Administrator.Services.Database;
using Discord;
using Discord.Commands;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using MoreLinq;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Administrator.Modules.BotOwner
{
    public class Globals
    {
        public SocketCommandContext Context { get; set; }
        public DbService Db { get; set; }
    }    

    [Name("BotOwner")]
    public class BotOwnerCommands : ModuleBase<SocketCommandContext>
    {
        private static readonly Config Config = BotConfig.New();
        private readonly DbService _db;
        private readonly CommandService _commands;

        public BotOwnerCommands(DbService db, CommandService commands)
        {
            _db = db;
            _commands = commands;
        }

        [Command("gencommands")]
        [Summary("generates the commandlist.md.")]
        [Usage("{p}gencommands")]
        [RequireOwner]
        private async Task GenerateCommandlistAsync()
        {
            var text = string.Empty;
            var modules = _commands.Modules.Where(x => !x.Name.Equals("BotOwner")).ToList();
            foreach (var module in modules)
            {
                text += $"## {module.Name}\n" +
                        "|Command|Summary|Usage|\n" +
                        "|---|---|---|\n";// +
                        //"|-| -------| ------------------------------------------\n";
                var commands = module.Commands.DistinctBy(x => x.Name).ToList();
                foreach (var command in commands)
                {
                    var name = $"`{Config.BotPrefix}{command.Name}`";
                    var aliases = command.Aliases.Any(x => !x.Equals(command.Name))
                        ? string.Join(string.Empty,
                            command.Aliases.Where(x => !x.Equals(command.Name))
                                .Select(x => $"<br>`{Config.BotPrefix}{x}`"))
                        : "<br>";
                    var summary = command.Summary.Replace("{p}", Config.BotPrefix).Replace("\n", "<br>");
                    var usage = string.Join("<br>",
                        (command.Attributes.FirstOrDefault(x => x is UsageAttribute) as UsageAttribute)?.Text
                        .Select(x => $"`{x}`".Replace("{p}", Config.BotPrefix)));
                    text +=$"|{name}{aliases}|{summary}|{usage}|\n";
                }
            }

            try
            {
                if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "Data/commandlist.md")))
                {
                    File.Delete(Path.Combine(Directory.GetCurrentDirectory(), "Data/commandlist.md"));
                }
                
                await File.WriteAllTextAsync(Path.Combine(Directory.GetCurrentDirectory(), "Data/commandlist.md"), text)
                    .ConfigureAwait(false);
                await Context.Channel.SendConfirmAsync("Command list generated.");
                await Context.Channel.SendFileAsync(Path.Combine(Directory.GetCurrentDirectory(), "Data/commandlist.md"))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Context.Channel.SendErrorAsync($"```\n{ex}\n```").ConfigureAwait(false);
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
                var remainder = string.Join(" ", Enumerable.TakeLast(args, args.Length - 1));
                await Context.Client.SetGameAsync(remainder, type: type).ConfigureAwait(false);
                await Context.Message.AddReactionAsync(new Emoji("\U00002705")).ConfigureAwait(false);
            }
            else
            {
                await Context.Message.AddReactionAsync(new Emoji("\U0000274c")).ConfigureAwait(false);
            }
        }

        [Command("eval", RunMode = RunMode.Async)]
        [RequireOwner]
        private async Task EvalCodeAsync([Remainder] string script)
        {
            try
            {
                var sopts = ScriptOptions.Default;
                sopts = sopts.WithImports("System", "System.Collections.Generic", "System.Linq", "System.Text",
                    "System.Threading.Tasks", "Discord", "Discord.Commands", "Administrator.Extensions", "Administrator.Services.Database.Models");
                sopts = sopts.WithReferences(AppDomain.CurrentDomain.GetAssemblies().Where(x => !x.IsDynamic && !string.IsNullOrWhiteSpace(x.Location)));
                var result = await CSharpScript.EvaluateAsync(SanitizeCode(script), sopts, new Globals{ Context = Context, Db = _db})
                    .ConfigureAwait(false);
                if (!(result is null))
                {
                    await Context.Channel.SendConfirmAsync(result.ToString()).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                await Context.Channel.SendErrorAsync($"Could not execute code:\n```\n{ex}\n```").ConfigureAwait(false);
            }

            string SanitizeCode(string s)
            {
                var cleanCode = s.Replace("```csharp", string.Empty).Replace("```cs", string.Empty).Replace("```", string.Empty);
                return Regex.Replace(cleanCode.Trim(), "^`|`$", string.Empty); //strip out the ` characters from the beginning and end of the string
            }
        }

        /*
        [Command("eval", RunMode = RunMode.Async)]
        [RequireOwner]
        private async Task EvalCodeAsync([Remainder] string script)
        {
            try
            {
                var sopts = ScriptOptions.Default;
                sopts = sopts.WithImports("System", "System.Collections.Generic", "System.Linq", "System.Text",
                    "System.Threading.Tasks", "Discord", "Discord.Commands", "Administrator.Extensions");
                sopts = sopts.WithReferences(AppDomain.CurrentDomain.GetAssemblies().Where(x => !x.IsDynamic && !string.IsNullOrWhiteSpace(x.Location)));
                var result = await CSharpScript.EvaluateAsync(SanitizeCode(script), sopts, this)
                    .ConfigureAwait(false);
                if (!(result is null))
                {
                    await Context.Channel.SendConfirmAsync(result.ToString()).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                await Context.Channel.SendErrorAsync($"Could not execute code:\n```\n{ex}\n```").ConfigureAwait(false);
            }

            string SanitizeCode(string s)
            {
                var cleanCode = s.Replace("```csharp", string.Empty).Replace("```cs", string.Empty).Replace("```", string.Empty);
                return Regex.Replace(cleanCode.Trim(), "^`|`$", string.Empty); //strip out the ` characters from the beginning and end of the string
            }
        }
        */

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