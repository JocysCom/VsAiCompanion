using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using System;
using System.Collections;
using System.ComponentModel;
using System.IO;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
  public class SqlFileConnectionProperties : SqlConnectionProperties
  {
    private string _defaultDataSource;

    public SqlFileConnectionProperties()
      : this((string) null)
    {
    }

    public SqlFileConnectionProperties(string defaultInstanceName)
    {
      this._defaultDataSource = ".";
      if (defaultInstanceName != null && defaultInstanceName.Length > 0)
        this._defaultDataSource = this._defaultDataSource + "\\" + defaultInstanceName;
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
        return base.IsComplete && this.ConnectionStringBuilder["AttachDbFilename"] is string && (this.ConnectionStringBuilder["AttachDbFilename"] as string).Length != 0;
      }
    }

    public override void Test()
    {
      string path = this.ConnectionStringBuilder["AttachDbFilename"] as string;
      try
      {
        this.ConnectionStringBuilder["AttachDbFilename"] = path != null && path.Length != 0 ? (object) Path.GetFullPath(path) : throw new InvalidOperationException(SR.SqlFileConnectionProperties_NoFileSpecified);
        if (!File.Exists(this.ConnectionStringBuilder["AttachDbFilename"] as string))
          throw new InvalidOperationException(SR.SqlFileConnectionProperties_CannotTestNonExistentMdf);
        base.Test();
      }
      catch (SqlException ex)
      {
        if (ex.Number == -2)
          throw new ApplicationException(ex.Errors[0].Message + Environment.NewLine + SR.SqlFileConnectionProperties_TimeoutReasons);
        throw;
      }
      finally
      {
        if (path != null && path.Length > 0)
          this.ConnectionStringBuilder["AttachDbFilename"] = (object) path;
      }
    }

    protected override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
    {
      PropertyDescriptorCollection properties1 = base.GetProperties(attributes);
      PropertyDescriptor baseDescriptor = properties1.Find("DataSource", true);
      if (baseDescriptor != null)
      {
        int index = properties1.IndexOf(baseDescriptor);
        PropertyDescriptor[] properties2 = new PropertyDescriptor[properties1.Count];
        properties1.CopyTo((Array) properties2, 0);
        properties2[index] = (PropertyDescriptor) new DynamicPropertyDescriptor(baseDescriptor, new Attribute[1]
        {
          (Attribute) new TypeConverterAttribute(typeof (SqlFileConnectionProperties.DataSourceConverter))
        });
        (properties2[index] as DynamicPropertyDescriptor).CanResetValueHandler = new CanResetValueHandler(this.CanResetDataSource);
        (properties2[index] as DynamicPropertyDescriptor).ResetValueHandler = new ResetValueHandler(this.ResetDataSource);
        properties1 = new PropertyDescriptorCollection(properties2, true);
      }
      return properties1;
    }

    private void LocalReset()
    {
      this["Data Source"] = (object) this._defaultDataSource;
      this["User Instance"] = (object) true;
      this["Connection Timeout"] = (object) 30;
    }

    private bool CanResetDataSource(object component)
    {
      return !(this["Data Source"] is string) || !(this["Data Source"] as string).Equals(this._defaultDataSource, StringComparison.OrdinalIgnoreCase);
    }

    private void ResetDataSource(object component)
    {
      this["Data Source"] = (object) this._defaultDataSource;
    }

    private class DataSourceConverter : StringConverter
    {
      private TypeConverter.StandardValuesCollection _standardValues;

      public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;

      public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) => true;

      public override TypeConverter.StandardValuesCollection GetStandardValues(
        ITypeDescriptorContext context)
      {
        if (this._standardValues == null)
        {
          RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Microsoft SQL Server\\Instance Names\\SQL");
          if (registryKey != null)
          {
            string[] values = (string[]) null;
            using (registryKey)
              values = registryKey.GetValueNames();
            for (int index = 0; index < values.Length; ++index)
              values[index] = ".\\" + values[index];
            this._standardValues = new TypeConverter.StandardValuesCollection((ICollection) values);
          }
          else
            this._standardValues = new TypeConverter.StandardValuesCollection((ICollection) new string[0]);
        }
        return this._standardValues;
      }
    }
  }
}
