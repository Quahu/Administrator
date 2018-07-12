using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Administrator.Common;
using Administrator.Common.Attributes;
using Administrator.Extensions;
using Discord;
using Discord.Commands;

namespace Administrator.Modules.Fun
{
    [Name("Fun")]
    [RequirePermissionsPass]
    public class FunCommands : AdminBase
    {
        public HttpClient Http { get; set; }

        [Command("big")]
        [Alias("expand")]
        [Summary("Blow up an emote or emoji.")]
        [Usage("big 😩")]
        private async Task<RuntimeResult> BlowUpEmoteAsync([Remainder] string emoteStr)
        {
            if (Emote.TryParse(emoteStr, out var result))
            {
                try
                {
                    var stream = await Http.GetStreamAsync(result.Url);
                    await Context.Channel.SendFileAsync(stream, $"{result.Id}.{(result.Animated ? "gif" : "png")}");
                }
                catch
                {
                    // ignored
                }

                return await CommandSuccess();
            }

            try
            {
                if (!emoteStr.IsEmoji()) return await CommandError("Input is not an emoji.");
                var codePoints = StringExtensions.GetUnicodeCodePoints(emoteStr).ToList();
                if (!codePoints.Any()) return await CommandError("Emoji has no valid codepoints.");
                var filename = $"{string.Join('-', codePoints.Select(x => x.ToString("X2"))).ToLower()}";
                var stream = await Http.GetStreamAsync(
                    $"https://i.kuro.mu/emoji/256x256/{filename}.png");
                await Context.Channel.SendFileAsync(stream, $"{filename}.png");
                return await CommandSuccess();
            }
            catch
            {
                return await CommandError("Emoji or image link not valid.");
            }
        }
    }
}
