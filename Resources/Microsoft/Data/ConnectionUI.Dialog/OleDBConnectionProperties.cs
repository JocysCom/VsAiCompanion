using System;
using System.ComponentModel;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
  public class OleDBConnectionProperties : AdoDotNetConnectionProperties
  {
    private bool _disableProviderSelection;

    public OleDBConnectionProperties()
      : base("System.Data.OleDb")
    {
    }

    public bool DisableProviderSelection
    {
      get => this._disableProviderSelection;
      set => this._disableProviderSelection = value;
    }

    public override bool IsComplete
    {
      get
      {
        return this.ConnectionStringBuilder["Provider"] is string && (this.ConnectionStringBuilder["Provider"] as string).Length != 0;
      }
    }

    protected override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
    {
      PropertyDescriptorCollection properties1 = base.GetProperties(attributes);
      if (this._disableProviderSelection)
      {
        PropertyDescriptor baseDescriptor = properties1.Find("Provider", true);
        if (baseDescriptor != null)
        {
          int index = properties1.IndexOf(baseDescriptor);
          PropertyDescriptor[] properties2 = new PropertyDescriptor[properties1.Count];
          properties1.CopyTo((Array) properties2, 0);
          properties2[index] = (PropertyDescriptor) new DynamicPropertyDescriptor(baseDescriptor, new Attribute[1]
          {
            (Attribute) ReadOnlyAttribute.Yes
          });
          (properties2[index] as DynamicPropertyDescriptor).CanResetValueHandler = new CanResetValueHandler(this.CanResetProvider);
          properties1 = new PropertyDescriptorCollection(properties2, true);
        }
      }
      return properties1;
    }

    private bool CanResetProvider(object component) => false;
  }
}
