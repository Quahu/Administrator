using System;
using System.Collections.Generic;
using System.Text;
using Administrator.Common.Database;
using Discord.Commands;
using Discord.WebSocket;

namespace Administrator.Common
{
    public class AdminCommandContext : SocketCommandContext
    {
        public AdminCommandContext(DiscordSocketClient client, SocketUserMessage msg, AdminContext ctx) : base(client, msg)
        {
            Database = ctx;
        }

        public AdminContext Database { get; }
    }
}
