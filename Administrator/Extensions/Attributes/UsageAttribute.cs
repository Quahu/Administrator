using System;

namespace Administrator.Extensions.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class UsageAttribute : Attribute
    {
        public UsageAttribute(params string[] text)
        {
            Text = text;
        }

        public string[] Text { get; }
    }
}