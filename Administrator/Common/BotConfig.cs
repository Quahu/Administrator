using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Nett;
using Discord;
using NLog;

namespace Administrator.Common
{
    public static class BotConfig
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static string _connectionString;

        public static void Create()
        {
            try
            {
                var config = Toml.ReadFile<Config>(Path.Combine(Directory.GetCurrentDirectory(), "Data/Config.toml"));

                if (config is null) throw new NullReferenceException("Configuration file is not properly formatted.");
                if (string.IsNullOrWhiteSpace(config.Token)) throw new ArgumentNullException(nameof(config.Token), "Bot token is not set.");
                if (string.IsNullOrWhiteSpace(config.Prefix)) throw new ArgumentNullException(nameof(config.Prefix), "Bot prefix is empty or missing.");
                if (string.IsNullOrWhiteSpace(config.ImgurId)) throw new ArgumentNullException(nameof(config.ImgurId), "Imgur API ID is not set.");
                if (string.IsNullOrWhiteSpace(config.ImgurSecret)) throw new ArgumentNullException(nameof(config.ImgurSecret), "Imgur API secret is not set.");
                if (!Enum.TryParse(config.DatabaseType, true, out DatabaseType dbType))
                    throw new ArgumentException(
                        $"Could not parse database type. Valid options are: SQLite, PostgreSQL.",
                        nameof(config.DatabaseType));
                if (string.IsNullOrWhiteSpace(config.ConnectionString)) throw new ArgumentNullException(nameof(config.ConnectionString), "Database connection string is not set.");
                if (config.OwnerIds is null || !config.OwnerIds.Any()) Log.Warn("No owner IDs detected. Any bot owner commands will be unusable.");
                if (!uint.TryParse(config.OkColor, NumberStyles.HexNumber, null, out var okColor))
                    throw new ArgumentNullException(nameof(config.OkColor), "Ok color was not in a correct format.");
                if (!uint.TryParse(config.ErrorColor, NumberStyles.HexNumber, null, out var errorColor))
                    throw new ArgumentNullException(nameof(config.OkColor), "Error color was not in a correct format.");
                if (!uint.TryParse(config.WarnColor, NumberStyles.HexNumber, null, out var warnColor))
                    throw new ArgumentNullException(nameof(config.OkColor), "Warn color was not in a correct format.");
                if (!uint.TryParse(config.WinColor, NumberStyles.HexNumber, null, out var winColor))
                    throw new ArgumentNullException(nameof(config.OkColor), "Win color was not in a correct format.");
                if (!uint.TryParse(config.LoseColor, NumberStyles.HexNumber, null, out var loseColor))
                    throw new ArgumentNullException(nameof(config.OkColor), "Lose color was not in a correct format.");

                Token = config.Token;
                Prefix = config.Prefix;
                ImgurId = config.ImgurId;
                ImgurSecret = config.ImgurSecret;
                DatabaseType = dbType;
                _connectionString = config.ConnectionString;
                OwnerIds = config.OwnerIds.ConvertAll(x => (ulong) x);
                OkColor = new Color(okColor);
                ErrorColor = new Color(errorColor);
                WarnColor = new Color(warnColor);
                WinColor = new Color(winColor);
                LoseColor = new Color(loseColor);
            }
            catch (Exception ex)
            {
                Log.Fatal("There was a problem creating the bot configuation - a restart is required.");
                Log.Fatal(ex, ex.ToString);
                Console.ReadKey();
                Environment.Exit(-1);
            }
        }

        public const ushort PREFIX_MAX_LENGTH = 25;
        public const ushort PHRASE_MIN_LENGTH = 3;
        public const string INVITE_REGEX_PATTERN = @"discord(?:\.com|\.gg)[\/invite\/]?(?:(?!.*[Ii10OolL]).[a-zA-Z0-9]{5,6}|[a-zA-Z0-9\-]{2,32})";

        public static string Token { get; private set; }

        public static string Prefix { get; private set; }

        public static List<ulong> OwnerIds { get; private set; }

        public static string ImgurId { get; private set; }

        public static string ImgurSecret { get; private set; }

        public static DatabaseType DatabaseType { get; private set; } = DatabaseType.SQLite;

        public static string ConnectionString
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_connectionString))
                {
                    Create();
                }

                return _connectionString;
            }
        }

        public static Color OkColor { get; private set; }

        public static Color ErrorColor { get; private set; }

        public static Color WarnColor { get; private set; }

        public static Color WinColor { get; private set; }

        public static Color LoseColor { get; private set; }

    }

    internal class Config
    {
        public string Token { get; private set; }

        public string Prefix { get; private set; }

        public List<long> OwnerIds { get; private set; }

        public string ImgurId { get; private set; }

        public string ImgurSecret { get; private set; }

        public string DatabaseType { get; private set; }
        
        public string ConnectionString { get; private set; }

        public string OkColor { get; private set; }

        public string ErrorColor { get; private set; }

        public string WarnColor { get; private set; }

        public string WinColor { get; private set; }

        public string LoseColor { get; private set; }
    }
}
