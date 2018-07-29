using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Administrator.Common;
using Administrator.Common.Attributes;
using Administrator.Extensions;
using Administrator.Services;
using Discord;
using Discord.Commands;

namespace Administrator.Modules.Utility
{
    [Name("Utility")]
    [RequirePermissionsPass]
    public class UtilityCommands : AdminBase
    {
        public HttpClient Http { get; set; }

        [Command("say")]
        [Summary("Send a message in a specified channel (defaults to the current channel). Supports TOML embeds.")]
        [Usage("say #somechannel no u", "say no memes")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        private async Task<RuntimeResult> SayMessageAsync(IMessageChannel channel, [Remainder] string message = "")
        {
            if (string.IsNullOrWhiteSpace(message) && !Context.Message.Attachments.Any()) return await CommandError("Cannot send an empty message.");

            if (TomlEmbed.TryParse(message, out var result))
            {
                if (Context.Message.Attachments.FirstOrDefault() is Attachment a)
                {
                    var stream = await Http.GetStreamAsync(a.Url);
                    await channel.SendFileAsync(stream, a.Filename, result.Plaintext ?? string.Empty, embed: result.ToEmbed());
                    return await CommandSuccess();
                }

                await channel.EmbedAsync(result);
                if (channel.Id == Context.Channel.Id) await Context.Message.TryDeleteAsync();
                return await CommandSuccess();
            }

            if (Context.Message.Attachments.FirstOrDefault() is Attachment at)
            {
                var stream = await Http.GetStreamAsync(at.Url);
                await channel.SendFileAsync(stream, at.Filename, message);
                if (channel.Id == Context.Channel.Id) await Context.Message.TryDeleteAsync();
                return await CommandSuccess();
            }

            await channel.SendMessageAsync(message);
            if (channel.Id == Context.Channel.Id) await Context.Message.TryDeleteAsync();
            return await CommandSuccess();
        }

        [Command("say")]
        [Summary("Send a message in a specified channel (defaults to the current channel). Supports TOML embeds.")]
        [Usage("say #somechannel no u", "say no memes")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        private async Task<RuntimeResult> SayMessageAsync([Remainder] string message = "")
            => await SayMessageAsync(Context.Channel, message);
    }
}
