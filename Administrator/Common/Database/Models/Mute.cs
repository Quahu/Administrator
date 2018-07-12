using System;

namespace Administrator.Common.Database.Models
{
    public class Mute : Infraction
    {
        public TimeSpan? Duration { get; set; }

        public bool HasExpired
            => Duration is TimeSpan ts ? DateTimeOffset.UtcNow > Timestamp + ts : false;

        public bool CanBeAppealed
            => Duration is TimeSpan ts ? ts > TimeSpan.FromHours(24) : true;
    }
}
