using System;
using System.ComponentModel;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
  public class OleDBSpecializedConnectionProperties : OleDBConnectionProperties
  {
    private string _provider;

    public OleDBSpecializedConnectionProperties(string provider)
    {
      this._provider = provider;
      this.LocalReset();
    }

    public override void Reset()
    {
      base.Reset();
      this.LocalReset();
    }

    protected override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
    {
      bool providerSelection = this.DisableProviderSelection;
      try
      {
        this.DisableProviderSelection = true;
        return base.GetProperties(attributes);
      }
      finally
      {
        this.DisableProviderSelection = providerSelection;
      }
    }

    private void LocalReset() => this["Provider"] = (object) this._provider;
  }
}
