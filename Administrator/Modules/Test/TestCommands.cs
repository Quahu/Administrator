using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Administrator.Common;
using Administrator.Services.Database;
using Discord.Commands;

namespace Administrator.Modules.Test
{
    public class TestCommands : AdminBase
    {
        public TestCommands(DbService db)
        {
            Db = db;
        }

        [Command("test")]
        private async Task TestAsync()
        {
            await SendConfirmAsync("test").ConfigureAwait(false);
        }
    }
}
