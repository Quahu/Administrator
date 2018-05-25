using Administrator.Extensions;
using System.Linq;
using System.Threading.Tasks;

namespace Administrator.Services.Scheduler.Schedules
{
    public class CrosstalkChecker
    {
        private readonly CrosstalkService _crosstalk;

        public CrosstalkChecker(CrosstalkService crosstalk)
        {
            _crosstalk = crosstalk;
        }

        public async Task CheckExpiredCallsAsync()
        {
            foreach (var c in _crosstalk.Calls.Where(x => x.IsExpired).ToList())
            {
                if (c.IsConnected)
                {
                    await c.Channel1.SendConfirmAsync("Hanging up the call - 2 minutes expired.").ConfigureAwait(false);
                    await c.Channel2.SendConfirmAsync("Hanging up the call - 2 minutes expired.").ConfigureAwait(false);
                    _crosstalk.Calls.Remove(c);
                    continue;
                }

                await c.Channel1.SendErrorAsync("Hanging up the call because nobody answered.").ConfigureAwait(false);
                _crosstalk.Calls.Remove(c);
            }
        }
    }
}
