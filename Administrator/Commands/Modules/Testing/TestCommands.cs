using System.Threading.Tasks;
using Qmmands;

namespace Administrator.Commands
{
    public sealed class TestCommands : AdminModuleBase
    {
        [Command("sayhi")]
        public Task<AdminCommandResult> SayHi()
            => CommandSuccess("test_hello", args: Context.User);
    }
}