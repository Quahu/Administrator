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

        private async Task BlahAsync([Remainder] string link)
        {
            try
            {
                var linkStream = await Http.GetStreamAsync(link);
                await Context.Client.CurrentUser.ModifyAsync(x => x.Avatar = new Image(linkStream));
            }
            catch
            {
                // ignored
            }
        }
    }
}
