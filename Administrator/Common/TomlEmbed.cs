using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Discord;
using Nett;

namespace Administrator.Common
{
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

        public Embed ToEmbed(IUser user = null, IGuild guild = null)
        {
            var eb = new EmbedBuilder();
            if (!string.IsNullOrWhiteSpace(Title))
            {
                eb.WithTitle(Title);
            }

            if (!string.IsNullOrWhiteSpace(Description))
            {
                eb.WithDescription(Description);
            }

            if (!string.IsNullOrWhiteSpace(Color)
                && uint.TryParse(Color, NumberStyles.HexNumber, null, out var result))
            {
                eb.WithColor(result);
            }

            if (!string.IsNullOrWhiteSpace(ThumbnailUrl)
                && Uri.IsWellFormedUriString(ThumbnailUrl, UriKind.Absolute))
            {
                eb.WithThumbnailUrl(ThumbnailUrl);
            }

            if (!string.IsNullOrWhiteSpace(ImageUrl)
                && Uri.IsWellFormedUriString(ImageUrl, UriKind.Absolute))
            {
                eb.WithImageUrl(ImageUrl);
            }

            if (Fields?.Any() == true)
            {
                Fields.ForEach(x => eb.AddField(x.Name, x.Value, x.Inline));
            }

            if (Footer is Footer f)
            {
                eb.WithFooter(f.Text, f.IconUrl);
            }

            if (Author is Author a)
            {
                eb.WithAuthor(a.Name, a.IconUrl);
            }

            return eb.Build();
        }

        public static bool TryParse(string input, out TomlEmbed result)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                result = null;
                return false;
            }

            try
            {
                result = Toml.ReadString<TomlEmbed>(input);
                return true;
            }
            catch
            {
                result = null;
                return false;
            }
        }
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
