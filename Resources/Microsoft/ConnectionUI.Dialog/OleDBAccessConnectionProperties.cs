using System;
using System.ComponentModel;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
  public class OleDBAccessConnectionProperties : OleDBSpecializedConnectionProperties
  {
    public OleDBAccessConnectionProperties()
      : base("Microsoft.Jet.OLEDB.4.0")
    {
    }

    public override bool IsComplete
    {
      get
      {
        return base.IsComplete && this.ConnectionStringBuilder["Data Source"] is string && (this.ConnectionStringBuilder["Data Source"] as string).Length != 0;
      }
    }

    public override string ToDisplayString()
    {
      string str = (string) null;
      if (this.ConnectionStringBuilder.ContainsKey("Jet OLEDB:Database Password") && this.ConnectionStringBuilder.ShouldSerialize("Jet OLEDB:Database Password"))
      {
        str = this.ConnectionStringBuilder["Jet OLEDB:Database Password"] as string;
        this.ConnectionStringBuilder.Remove("Jet OLEDB:Database Password");
      }
      string displayString = base.ToDisplayString();
      if (str == null)
        return displayString;
      this.ConnectionStringBuilder["Jet OLEDB:Database Password"] = (object) str;
      return displayString;
    }

    protected override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
    {
      PropertyDescriptorCollection properties1 = base.GetProperties(attributes);
      PropertyDescriptor baseDescriptor = properties1.Find("Jet OLEDB:Database Password", true);
      if (baseDescriptor != null)
      {
        int index = properties1.IndexOf(baseDescriptor);
        PropertyDescriptor[] properties2 = new PropertyDescriptor[properties1.Count];
        properties1.CopyTo((Array) properties2, 0);
        properties2[index] = (PropertyDescriptor) new DynamicPropertyDescriptor(baseDescriptor, new Attribute[1]
        {
          (Attribute) PasswordPropertyTextAttribute.Yes
        });
        properties1 = new PropertyDescriptorCollection(properties2, true);
      }
      return properties1;
    }
  }
}
