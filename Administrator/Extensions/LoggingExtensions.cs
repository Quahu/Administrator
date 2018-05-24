using Discord.Commands;
using NLog;

namespace Administrator.Extensions
{
    public static class LoggingExtensions
    {
        public static void CommandError(this Logger log, double elapsedSeconds, SocketCommandContext context,
            IResult result)
        {
            log.Warn($"Command errored after {elapsedSeconds}s" +
                     $"\n\t\tServer: {context.Guild?.Name + " [" + context.Guild?.Id + "]"}" +
                     $"\n\t\tChannel: {context.Channel.Name + " [" + context.Channel.Id + "]"}" +
                     $"\n\t\tUser: {context.Message.Author + " [" + context.Message.Author.Id + "]"}" +
                     $"\n\t\tMessage: {context.Message.Content}" +
                     $"\n\t\tReason: {result.ErrorReason}");
            if (result is ExecuteResult r)
            {
                log.Warn($"\n\t\tException: {r.Exception}");
            }
        }

        public static void CommandSuccess(this Logger log, double elapsedSeconds, SocketCommandContext context)
        {
            log.Info($"Command executed after {elapsedSeconds}s" +
                     $"\n\t\tServer: {context.Guild?.Name + " [" + context.Guild?.Id + "]"}" +
                     $"\n\t\tChannel: {context.Channel.Name + " [" + context.Channel.Id + "]"}" +
                     $"\n\t\tUser: {context.Message.Author + " [" + context.Message.Author.Id + "]"}" +
                     $"\n\t\tMessage: {context.Message.Content}");
        }
    }
}