﻿using System;
using System.ComponentModel;

namespace Microsoft.Data.ConnectionUI
{
  [AttributeUsage(AttributeTargets.All)]
  internal sealed class SRDescriptionAttribute : DescriptionAttribute
  {
    private bool replaced;

    public SRDescriptionAttribute(string description)
      : base(description)
    {
    }

    public override string Description
    {
      get
      {
        if (!this.replaced)
        {
          this.replaced = true;
          this.DescriptionValue = SR.GetString(base.Description);
        }
        return base.Description;
      }
    }
  }
}
