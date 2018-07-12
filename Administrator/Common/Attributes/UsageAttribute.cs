using System;
using System.Collections.Generic;
using System.Text;

namespace Administrator.Common.Attributes
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
