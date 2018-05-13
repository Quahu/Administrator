using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Administrator.Services;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Administrator.Extensions.Attributes
{
    /// <summary>
    ///     Sets how often a user is allowed to use this command
    ///     or any command in this module.
    /// </summary>
    /// <remarks>
    ///     This is backed by an in-memory collection
    ///     and will not persist with restarts.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
    public sealed class RatelimitAttribute : PreconditionAttribute
    {
        private readonly bool _applyPerGuild;
        private readonly uint _invokeLimit;
        private readonly TimeSpan _invokeLimitPeriod;

        private readonly Dictionary<(ulong, ulong?), CommandTimeout> _invokeTracker =
            new Dictionary<(ulong, ulong?), CommandTimeout>();

        private readonly bool _noLimitForAdmins;
        private readonly bool _noLimitInDMs;

        /// <summary> Sets how often a user is allowed to use this command. </summary>
        /// <param name="times">The number of times a user may use the command within a certain period.</param>
        /// <param name="period">The amount of time since first invoke a user has until the limit is lifted.</param>
        /// <param name="measure">The scale in which the <paramref name="period" /> parameter should be measured.</param>
        /// <param name="noLimitInDMs">Set whether or not there is no limit to the command in DMs. Defaults to false.</param>
        /// <param name="noLimitForAdmins">Set whether or not there is no limit to the command for guild admins. Defaults to false.</param>
        /// <param name="applyPerGuild">Set whether or not to apply a limit per guild. Defaults to false.</param>
        public RatelimitAttribute(uint times, double period, Measure measure, bool noLimitInDMs = false,
            bool noLimitForAdmins = false, bool applyPerGuild = false)
        {
            _invokeLimit = times;
            _noLimitInDMs = noLimitInDMs;
            _noLimitForAdmins = noLimitForAdmins;
            _applyPerGuild = applyPerGuild;

            //TODO: C# 7 candidate switch expression
            switch (measure)
            {
                case Measure.Days:
                    _invokeLimitPeriod = TimeSpan.FromDays(period);
                    break;
                case Measure.Hours:
                    _invokeLimitPeriod = TimeSpan.FromHours(period);
                    break;
                case Measure.Minutes:
                    _invokeLimitPeriod = TimeSpan.FromMinutes(period);
                    break;
            }
        }

        /// <summary> Sets how often a user is allowed to use this command. </summary>
        /// <param name="times">The number of times a user may use the command within a certain period.</param>
        /// <param name="period">The amount of time since first invoke a user has until the limit is lifted.</param>
        /// <param name="noLimitInDMs">Set whether or not there is no limit to the command in DMs. Defaults to false.</param>
        /// <param name="noLimitForAdmins">Set whether or not there is no limit to the command for guild admins. Defaults to false.</param>
        /// <param name="applyPerGuild">Set whether or not to apply a limit per guild. Defaults to false.</param>
        public RatelimitAttribute(uint times, TimeSpan period, bool noLimitInDMs = false, bool noLimitForAdmins = false,
            bool applyPerGuild = false)
        {
            _invokeLimit = times;
            _noLimitInDMs = noLimitInDMs;
            _noLimitForAdmins = noLimitForAdmins;
            _invokeLimitPeriod = period;
            _applyPerGuild = applyPerGuild;
        }

        /// <inheritdoc />
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context,
            CommandInfo command, IServiceProvider services)
        {
            var logging = services.GetService<LoggingService>();

            if (_noLimitInDMs && context.Channel is IPrivateChannel)
                return PreconditionResult.FromSuccess();

            if (_noLimitForAdmins && context.User is IGuildUser gu && gu.GuildPermissions.Administrator)
                return PreconditionResult.FromSuccess();

            var now = DateTimeOffset.UtcNow;
            var key = _applyPerGuild ? (context.User.Id, context.Guild?.Id) : (context.User.Id, null);

            var timeout = _invokeTracker.TryGetValue(key, out var t)
                          && now - t.FirstInvoke < _invokeLimitPeriod
                ? t
                : new CommandTimeout(now);

            timeout.TimesInvoked++;

            if (timeout.TimesInvoked <= _invokeLimit)
            {
                _invokeTracker[key] = timeout;
                return PreconditionResult.FromSuccess();
            }

            var timeleft = _invokeLimitPeriod - (now - timeout.FirstInvoke);
            var timeStr =
                $"{(timeleft.Minutes == 0 ? string.Empty : $"{timeleft.Minutes}m")}{timeleft.Seconds}s";
            logging.AddIgnoredMessages(new List<IMessage> {context.Message});

            if (context.Message.MentionedUserIds.Count == 0)
            {
                try
                {
                    await context.Message.DeleteAsync().ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }

            try
            {
                var dm = await context.User.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                await dm.SendMessageAsync($"You're still on cooldown. Idiot. Try again in {timeStr}.")
                    .ConfigureAwait(false);
                return PreconditionResult.FromError("User currently on cooldown for command - DM sent.");
            }
            catch
            {
                return PreconditionResult.FromError($"User currently on cooldown for command - could not send DM.");
            }

        }

        private class CommandTimeout
        {
            public CommandTimeout(DateTimeOffset timeStarted)
            {
                FirstInvoke = timeStarted;
            }

            public uint TimesInvoked { get; set; }
            public DateTimeOffset FirstInvoke { get; }
        }
    }

    /// <summary> Sets the scale of the period parameter. </summary>
    public enum Measure
    {
        /// <summary> Period is measured in days. </summary>
        Days,

        /// <summary> Period is measured in hours. </summary>
        Hours,

        /// <summary> Period is measured in minutes. </summary>
        Minutes
    }
}