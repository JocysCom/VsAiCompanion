using System;
using System.ComponentModel;

namespace Microsoft.Data.ConnectionUI
{
  [AttributeUsage(AttributeTargets.All)]
  internal sealed class SRCategoryAttribute : CategoryAttribute
  {
    public SRCategoryAttribute(string category)
      : base(category)
    {
    }

    protected override string GetLocalizedString(string value) => SR.GetString(value);
  }
}
