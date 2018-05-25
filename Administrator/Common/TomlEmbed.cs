using Discord;
using Nett;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Administrator.Common
{
    public static class TomlEmbedBuilder
    {
        public static TomlEmbed ReadToml(string toml)
            => Toml.ReadString<TomlEmbed>(toml);

        public static async Task<IUserMessage> EmbedAsync(this IMessageChannel channel, TomlEmbed embed, TimeSpan? timeout = null)
        {
            var plaintext = string.IsNullOrWhiteSpace(embed.Plaintext) ? string.Empty : embed.Plaintext;
            var eb = new EmbedBuilder();

            if (!string.IsNullOrWhiteSpace(embed.Title))
                eb.WithTitle(embed.Title);

            if (!string.IsNullOrWhiteSpace(embed.Description))
                eb.WithDescription(embed.Description);

            if (!string.IsNullOrWhiteSpace(embed.Color))
                eb.WithColor(Convert.ToUInt32(embed.Color, 16));

            if (!string.IsNullOrWhiteSpace(embed.ThumbnailUrl))
                eb.WithThumbnailUrl(embed.ThumbnailUrl);

            if (!string.IsNullOrWhiteSpace(embed.ImageUrl))
                eb.WithImageUrl(embed.ImageUrl);

            if (embed.Author is Author au)
                eb.WithAuthor(au.Name, au.IconUrl);

            if (embed.Footer is Footer f)
                eb.WithFooter(f.Text, f.IconUrl);

            if (embed.Fields is null || !embed.Fields.Any()) 
                return await channel.SendMessageAsync(plaintext, embed: eb.Build()).ConfigureAwait(false);

            foreach (var field in embed.Fields)
            {
                eb.AddField(field.Name, field.Value, field.Inline);
            }

            return await channel.SendMessageAsync(plaintext, embed: eb.Build()).ConfigureAwait(false);
        }

        public static (string, EmbedBuilder) ToMessage(this TomlEmbed embed)
        {
            var plaintext = string.IsNullOrWhiteSpace(embed.Plaintext) ? string.Empty : embed.Plaintext;
            var eb = new EmbedBuilder();

            if (!string.IsNullOrWhiteSpace(embed.Title))
                eb.WithTitle(embed.Title);

            if (!string.IsNullOrWhiteSpace(embed.Description))
                eb.WithDescription(embed.Description);

            if (!string.IsNullOrWhiteSpace(embed.Color))
                eb.WithColor(Convert.ToUInt32(embed.Color, 16));

            if (!string.IsNullOrWhiteSpace(embed.ThumbnailUrl))
                eb.WithThumbnailUrl(embed.ThumbnailUrl);

            if (!string.IsNullOrWhiteSpace(embed.ImageUrl))
                eb.WithImageUrl(embed.ImageUrl);

            if (embed.Author is Author au)
                eb.WithAuthor(au.Name, au.IconUrl);

            if (embed.Footer is Footer f)
                eb.WithFooter(f.Text, f.IconUrl);

            if (embed.Fields is null || !embed.Fields.Any()) 
                return (plaintext, eb);

            foreach (var field in embed.Fields.ToList())
            {
                eb.AddField(field.Name, field.Value, field.Inline);
            }

            return (plaintext, eb);
        }
    }

    public class TomlEmbed
    {
        public string Plaintext { get; private set; }

        public string Title { get; private set; }

        public string Description { get; private set; }

        public string Color { get; private set; }

        public string ThumbnailUrl { get; private set; }

        public string ImageUrl { get; private set; }

        public List<Field> Fields { get; private set; }

        public Footer Footer { get; private set; }

        public Author Author { get; private set; }
    }

    public class Footer
    {
        public string Text { get; private set; }
        
        public string IconUrl { get; private set; }
    }

    public class Author
    {
        public string Name { get; private set; }

        public string IconUrl { get; private set; }
    }

    public class Field
    {
        public string Name { get; private set; }

        public string Value { get; private set; }

        public bool Inline { get; private set; }
    }
}
