using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Administrator.Common;
using Administrator.Extensions;
using Administrator.Extensions.Attributes;
using Administrator.Services;
using Administrator.Services.Database;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Administrator.Modules.BotOwner
{

    [Name("BotOwner")]
    public class BotOwnerCommands : ModuleBase<SocketCommandContext>
    {
        public class Globals
        {
            public SocketCommandContext Context;
            public DbService Db;
        }

        private static readonly Config Config = BotConfig.New();
        private readonly DbService _db;

        public BotOwnerCommands(DbService db)
        {
            _db = db;
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