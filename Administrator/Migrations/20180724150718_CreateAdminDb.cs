using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Administrator.Migrations
{
    public partial class CreateAdminDb : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiscordUsers",
                columns: table => new
                {
                    Id = table.Column<ulong>(nullable: false),
                    GlobalXp = table.Column<uint>(nullable: false),
                    LastXpGain = table.Column<DateTimeOffset>(nullable: false),
                    RespectsPaid = table.Column<uint>(nullable: false),
                    LastRespectsPaid = table.Column<DateTimeOffset>(nullable: false),
                    AllowSuggestionDms = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscordUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuildConfigs",
                columns: table => new
                {
                    Id = table.Column<ulong>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    Prefix = table.Column<string>(nullable: true),
                    PermRoleId = table.Column<ulong>(nullable: true),
                    MuteRole = table.Column<int>(nullable: false),
                    MuteRoleId = table.Column<ulong>(nullable: false),
                    LtpRole = table.Column<int>(nullable: false),
                    LtpRoleId = table.Column<ulong>(nullable: false),
                    LtpRoleTimeout = table.Column<TimeSpan>(nullable: true),
                    LogWarnChannelId = table.Column<ulong>(nullable: false),
                    LogAppealChannelId = table.Column<ulong>(nullable: false),
                    LogMuteChannelId = table.Column<ulong>(nullable: false),
                    LogJoinChannelId = table.Column<ulong>(nullable: false),
                    LogLeaveChannelId = table.Column<ulong>(nullable: false),
                    LogBanChannelId = table.Column<ulong>(nullable: false),
                    LogUnbanChannelId = table.Column<ulong>(nullable: false),
                    LogMessageDeletionChannelId = table.Column<ulong>(nullable: false),
                    LogMessageUpdatedChannelId = table.Column<ulong>(nullable: false),
                    Suggestions = table.Column<int>(nullable: false),
                    SuggestionChannelId = table.Column<ulong>(nullable: false),
                    ArchiveSuggestions = table.Column<int>(nullable: false),
                    SuggestionArchiveId = table.Column<ulong>(nullable: false),
                    Greetings = table.Column<int>(nullable: false),
                    GreetChannelId = table.Column<ulong>(nullable: false),
                    GreetTimeout = table.Column<TimeSpan>(nullable: true),
                    GreetMessage = table.Column<string>(nullable: true),
                    UpvoteArrow = table.Column<string>(nullable: true),
                    DownvoteArrow = table.Column<string>(nullable: true),
                    TrackRespects = table.Column<int>(nullable: false),
                    FilterInvites = table.Column<int>(nullable: false),
                    VerboseErrors = table.Column<int>(nullable: false),
                    MinimumPhraseLength = table.Column<ushort>(nullable: false),
                    InviteCode = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Infractions",
                columns: table => new
                {
                    Id = table.Column<uint>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReceiverId = table.Column<ulong>(nullable: false),
                    ReceiverName = table.Column<string>(nullable: true),
                    IssuerId = table.Column<ulong>(nullable: false),
                    IssuerName = table.Column<string>(nullable: true),
                    Reason = table.Column<string>(nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(nullable: false),
                    HasBeenRevoked = table.Column<bool>(nullable: false),
                    RevokerId = table.Column<ulong>(nullable: false),
                    RevokerName = table.Column<string>(nullable: true),
                    RevocationTimestamp = table.Column<DateTimeOffset>(nullable: false),
                    AppealedTimestamp = table.Column<DateTimeOffset>(nullable: false),
                    AppealMessage = table.Column<string>(nullable: true),
                    GuildId = table.Column<ulong>(nullable: false),
                    Discriminator = table.Column<string>(nullable: false),
                    Duration = table.Column<TimeSpan>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Infractions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MessageFilters",
                columns: table => new
                {
                    Id = table.Column<uint>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<int>(nullable: false),
                    Filter = table.Column<string>(nullable: true),
                    GuildId = table.Column<ulong>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageFilters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<uint>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CommandOrModule = table.Column<string>(nullable: true),
                    Filter = table.Column<int>(nullable: false),
                    TypeId = table.Column<ulong>(nullable: true),
                    Type = table.Column<int>(nullable: false),
                    Functionality = table.Column<int>(nullable: false),
                    GuildId = table.Column<ulong>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarningPunishments",
                columns: table => new
                {
                    Id = table.Column<uint>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<ulong>(nullable: false),
                    Count = table.Column<uint>(nullable: false),
                    Type = table.Column<int>(nullable: false),
                    MuteDuration = table.Column<TimeSpan>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarningPunishments", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscordUsers");

            migrationBuilder.DropTable(
                name: "GuildConfigs");

            migrationBuilder.DropTable(
                name: "Infractions");

            migrationBuilder.DropTable(
                name: "MessageFilters");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "WarningPunishments");
        }
    }
}
