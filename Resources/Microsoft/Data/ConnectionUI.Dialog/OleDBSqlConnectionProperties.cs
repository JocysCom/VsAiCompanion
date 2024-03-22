using Microsoft.Win32;
using System;
using System.Collections;
using System.ComponentModel;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
  public class OleDBSqlConnectionProperties : OleDBSpecializedConnectionProperties
  {
    private static bool _sqlNativeClientRegistered;
    private static bool _gotSqlNativeClientRegistered;

    public OleDBSqlConnectionProperties()
      : base("SQLOLEDB")
    {
      this.LocalReset();
    }

    public override void Reset()
    {
      base.Reset();
      this.LocalReset();
    }

    public override bool IsComplete
    {
      get
      {
        return base.IsComplete && this.ConnectionStringBuilder["Data Source"] is string && (this.ConnectionStringBuilder["Data Source"] as string).Length != 0 && (this.ConnectionStringBuilder["Integrated Security"] != null && this.ConnectionStringBuilder["Integrated Security"].ToString().Equals("SSPI", StringComparison.OrdinalIgnoreCase) || this.ConnectionStringBuilder["User ID"] is string && (this.ConnectionStringBuilder["User ID"] as string).Length != 0);
      }
    }

    protected override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
    {
      PropertyDescriptorCollection properties = base.GetProperties(attributes);
      if (OleDBSqlConnectionProperties.SqlNativeClientRegistered && properties.Find("Provider", true) is DynamicPropertyDescriptor propertyDescriptor)
      {
        if (!this.DisableProviderSelection)
          propertyDescriptor.SetIsReadOnly(false);
        propertyDescriptor.SetConverterType(typeof (OleDBSqlConnectionProperties.SqlProviderConverter));
      }
      return properties;
    }

    private void LocalReset() => this["Integrated Security"] = (object) "SSPI";

    private static bool SqlNativeClientRegistered
    {
      get
      {
        if (!OleDBSqlConnectionProperties._gotSqlNativeClientRegistered)
        {
          RegistryKey registryKey = (RegistryKey) null;
          try
          {
            registryKey = Registry.ClassesRoot.OpenSubKey("SQLNCLI");
            OleDBSqlConnectionProperties._sqlNativeClientRegistered = registryKey != null;
          }
          finally
          {
            registryKey?.Close();
          }
          OleDBSqlConnectionProperties._gotSqlNativeClientRegistered = true;
        }
        return OleDBSqlConnectionProperties._sqlNativeClientRegistered;
      }
    }

    private class SqlProviderConverter : StringConverter
    {
      public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;

      public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) => true;

      public override TypeConverter.StandardValuesCollection GetStandardValues(
        ITypeDescriptorContext context)
      {
        return new TypeConverter.StandardValuesCollection((ICollection) new string[2]
        {
          "SQLOLEDB",
          "SQLNCLI"
        });
      }
    }
  }
}
