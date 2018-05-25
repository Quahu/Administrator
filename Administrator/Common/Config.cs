using Nett;
using System.Collections.Generic;
using System.IO;

namespace Administrator.Common
{
    public class BotConfig
    {
        public static Config New()
            => Toml.ReadFile<Config>(Path.Combine(Directory.GetCurrentDirectory(), "Data/Config.toml"));
    }

    public class Config
    {
        public string BotToken { get; private set; }

        public string BotPrefix { get; private set; }
        
        public List<long> OwnerIds { get; private set; }

        public Db Db { get; private set; }

        public Colors Colors { get; private set; }
    }

    public class Db
    {
        [TomlIgnore]
        public string FullLocation
            => Path.Combine(Directory.GetCurrentDirectory(), Location);

        public string Location { get; private set; }
    }

    public class Colors
    {
        public string Ok { get; private set; }

        public string Warn { get; private set; }

        public string Error { get; private set; }

        public string Win { get; private set; }

        public string Lose { get; private set; }
    }
}