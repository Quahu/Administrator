﻿// <auto-generated />
using System;
using Administrator.Common.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Administrator.Migrations
{
    [DbContext(typeof(AdminContext))]
    partial class AdminContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.1.0-rtm-30799");

            modelBuilder.Entity("Administrator.Common.Database.Models.DiscordUser", b =>
                {
                    b.Property<ulong>("Id");

                    b.Property<int>("AllowSuggestionDms");

                    b.Property<uint>("GlobalXp");

                    b.Property<DateTimeOffset>("LastRespectsPaid");

                    b.Property<DateTimeOffset>("LastXpGain");

                    b.Property<uint>("RespectsPaid");

                    b.HasKey("Id");

                    b.ToTable("DiscordUsers");
                });

            modelBuilder.Entity("Administrator.Common.Database.Models.GuildConfig", b =>
                {
                    b.Property<ulong>("Id");

                    b.Property<int>("ArchiveSuggestions");

                    b.Property<string>("DownvoteArrow");

                    b.Property<int>("FilterInvites");

                    b.Property<ulong>("GreetChannelId");

                    b.Property<string>("GreetMessage");

                    b.Property<TimeSpan?>("GreetTimeout");

                    b.Property<int>("Greetings");

                    b.Property<string>("InviteCode");

                    b.Property<ulong>("LogAppealChannelId");

                    b.Property<ulong>("LogBanChannelId");

                    b.Property<ulong>("LogJoinChannelId");

                    b.Property<ulong>("LogLeaveChannelId");

                    b.Property<ulong>("LogMessageDeletionChannelId");

                    b.Property<ulong>("LogMessageUpdatedChannelId");

                    b.Property<ulong>("LogMuteChannelId");

                    b.Property<ulong>("LogUnbanChannelId");

                    b.Property<ulong>("LogWarnChannelId");

                    b.Property<int>("LtpRole");

                    b.Property<ulong>("LtpRoleId");

                    b.Property<TimeSpan?>("LtpRoleTimeout");

                    b.Property<ushort>("MinimumPhraseLength");

                    b.Property<int>("MuteRole");

                    b.Property<ulong>("MuteRoleId");

                    b.Property<string>("Name");

                    b.Property<ulong?>("PermRoleId");

                    b.Property<string>("Prefix");

                    b.Property<ulong>("SuggestionArchiveId");

                    b.Property<ulong>("SuggestionChannelId");

                    b.Property<int>("Suggestions");

                    b.Property<int>("TrackRespects");

                    b.Property<string>("UpvoteArrow");

                    b.Property<int>("VerboseErrors");

                    b.HasKey("Id");

                    b.ToTable("GuildConfigs");
                });

            modelBuilder.Entity("Administrator.Common.Database.Models.Infraction", b =>
                {
                    b.Property<uint>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("AppealMessage");

                    b.Property<DateTimeOffset>("AppealedTimestamp");

                    b.Property<string>("Discriminator")
                        .IsRequired();

                    b.Property<ulong>("GuildId");

                    b.Property<bool>("HasBeenRevoked");

                    b.Property<ulong>("IssuerId");

                    b.Property<string>("IssuerName");

                    b.Property<string>("Reason");

                    b.Property<ulong>("ReceieverId");

                    b.Property<string>("ReceieverName");

                    b.Property<DateTimeOffset>("RevocationTimestamp");

                    b.Property<ulong>("RevokerId");

                    b.Property<string>("RevokerName");

                    b.Property<DateTimeOffset>("Timestamp");

                    b.HasKey("Id");

                    b.ToTable("Infractions");

                    b.HasDiscriminator<string>("Discriminator").HasValue("Infraction");
                });

            modelBuilder.Entity("Administrator.Common.Database.Models.MessageFilter", b =>
                {
                    b.Property<uint>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Filter");

                    b.Property<ulong>("GuildId");

                    b.Property<int>("Type");

                    b.HasKey("Id");

                    b.ToTable("MessageFilters");
                });

            modelBuilder.Entity("Administrator.Common.Database.Models.Permission", b =>
                {
                    b.Property<uint>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("CommandOrModule");

                    b.Property<int>("Filter");

                    b.Property<int>("Functionality");

                    b.Property<ulong?>("GuildId");

                    b.Property<int>("Type");

                    b.Property<ulong?>("TypeId");

                    b.HasKey("Id");

                    b.ToTable("Permissions");
                });

            modelBuilder.Entity("Administrator.Common.Database.Models.Warning", b =>
                {
                    b.Property<uint>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<ulong>("GuildId");

                    b.Property<ulong>("IssuerId");

                    b.Property<string>("Reason");

                    b.Property<ulong>("ReceiverId");

                    b.Property<DateTimeOffset>("Timestamp");

                    b.HasKey("Id");

                    b.ToTable("Warnings");
                });

            modelBuilder.Entity("Administrator.Common.Database.Models.WarningPunishment", b =>
                {
                    b.Property<uint>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<uint>("Count");

                    b.Property<ulong>("GuildId");

                    b.Property<TimeSpan?>("MuteDuration");

                    b.Property<int>("Type");

                    b.HasKey("Id");

                    b.ToTable("WarningPunishments");
                });

            modelBuilder.Entity("Administrator.Common.Database.Models.Ban", b =>
                {
                    b.HasBaseType("Administrator.Common.Database.Models.Infraction");


                    b.ToTable("Ban");

                    b.HasDiscriminator().HasValue("Ban");
                });

            modelBuilder.Entity("Administrator.Common.Database.Models.Mute", b =>
                {
                    b.HasBaseType("Administrator.Common.Database.Models.Infraction");

                    b.Property<TimeSpan?>("Duration");

                    b.ToTable("Mute");

                    b.HasDiscriminator().HasValue("Mute");
                });
#pragma warning restore 612, 618
        }
    }
}
