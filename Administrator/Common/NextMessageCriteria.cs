using System;
using System.Collections.Generic;
using System.Text;

namespace Administrator.Common
{
    [Flags]
    public enum NextMessageCriteria
    {
        None = 0,
        Guild = 1,
        Channel = 2,
        User = 4,
        GuildUser = Guild | User,
        ChannelUser = Channel | User
    }
}
