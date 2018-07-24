using System.IO;
using System.Linq;
using Administrator.Common.Database.Models;
using Discord;
using Microsoft.EntityFrameworkCore;

namespace Administrator.Common.Database
{
    public class AdminContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data source={Path.Combine(Directory.GetCurrentDirectory(), "Data/Administrator.db")}");
            /*
            switch (BotConfig.DatabaseType)
            {
                case DatabaseType.PostgreSQL:
                    optionsBuilder.UseNpgsql(BotConfig.ConnectionString);
                    break;
                case DatabaseType.SQLite:
                    optionsBuilder.UseSqlite(BotConfig.ConnectionString);
                    break;
                default:
                    optionsBuilder.UseSqlite(Path.Combine(Directory.GetCurrentDirectory(), "Data/Administrator.db"));
                    break;
            }
            */
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GuildConfig>()
                .HasKey(x => x.Id);
            modelBuilder.Entity<GuildConfig>()
                .Property(x => x.Id)
                .ValueGeneratedNever();

            modelBuilder.Entity<MessageFilter>()
                .HasKey(x => x.Id);
            modelBuilder.Entity<MessageFilter>()
                .Property(x => x.Id)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<DiscordUser>()
                .HasKey(x => x.Id);
            modelBuilder.Entity<DiscordUser>()
                .Property(x => x.Id)
                .ValueGeneratedNever();

            modelBuilder.Entity<Infraction>()
                .HasKey(x => x.Id);
            modelBuilder.Entity<Infraction>()
                .Property(x => x.Id)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<Ban>()
                .HasBaseType<Infraction>();

            modelBuilder.Entity<Mute>()
                .HasBaseType<Infraction>();

            modelBuilder.Entity<Warning>()
                .HasBaseType<Infraction>();

            modelBuilder.Entity<Permission>()
                .HasKey(x => x.Id);
            modelBuilder.Entity<Permission>()
                .Property(x => x.Id)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<WarningPunishment>()
                .HasKey(x => x.Id);
            modelBuilder.Entity<WarningPunishment>()
                .Property(x => x.Id)
                .ValueGeneratedOnAdd();
        }

        public DbSet<GuildConfig> GuildConfigs { get; set; }
        
        public DbSet<MessageFilter> MessageFilters { get; set; }

        public DbSet<DiscordUser> DiscordUsers { get; set; }

        public DbSet<Infraction> Infractions { get; set; }
        
        public DbSet<Permission> Permissions { get; set; }

        public DbSet<Warning> Warnings { get; set; }

        public DbSet<WarningPunishment> WarningPunishments { get; set; }

        public string GetPrefixOrDefault(IGuild guild)
        {
            if (guild is null) return BotConfig.Prefix;
            if (!(GuildConfigs.AsNoTracking().FirstOrDefault(x => x.Id == guild.Id) is GuildConfig gc)) return BotConfig.Prefix;
            return !string.IsNullOrWhiteSpace(gc.Prefix) ? gc.Prefix : BotConfig.Prefix;
        }

        public DiscordUser GetOrCreateDiscordUser(IUser user)
        {
            if (DiscordUsers.AsNoTracking().FirstOrDefault(x => x.Id == user.Id) is DiscordUser u) return u;

            var newUser = Add(new DiscordUser
            {
                Id = user.Id
            }).Entity;
            SaveChanges();
            return newUser;
        }

        public GuildConfig GetOrCreateGuildConfig(IGuild guild)
        {
            if (GuildConfigs.AsNoTracking().FirstOrDefault(x => x.Id == guild.Id) is GuildConfig g) return g;

            var gc = Add(new GuildConfig
            {
                Id = guild.Id,
                Name = guild.Name
            }).Entity;
            SaveChanges();
            return gc;
        }
    }
}
