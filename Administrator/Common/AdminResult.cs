using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace Administrator.Common
{
    public class AdminResult : RuntimeResult
    {
        private AdminResult(CommandError? error, string reason, TimeSpan executeTime, string message = null, Embed embed = null) : base(error, reason)
        {
            Message = message;
            ExecuteTime = executeTime;
            Embed = embed;
        }

        public string Message { get; }

        public TimeSpan ExecuteTime { get; }

        public Embed Embed { get; }

        public static AdminResult FromError(TimeSpan executeTime, string reason, string message = null, Embed embed = null)
            => new AdminResult(CommandError.Unsuccessful, reason, executeTime, message, embed);

        public static AdminResult FromSuccess(TimeSpan executeTime, string message = null, Embed embed = null)
            => new AdminResult(null, null, executeTime, message, embed);
    }
}
