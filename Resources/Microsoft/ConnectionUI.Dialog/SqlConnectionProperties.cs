using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
  public class SqlConnectionProperties : AdoDotNetConnectionProperties
  {
    private const int SqlError_CannotOpenDatabase = 4060;

    public SqlConnectionProperties()
      : base("Microsoft.Data.SqlClient")
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
        if (!(this.ConnectionStringBuilder["Data Source"] is string) || (this.ConnectionStringBuilder["Data Source"] as string).Length == 0)
          return false;
        return new HashSet<string>((IEnumerable<string>) new string[6]
        {
          "ActiveDirectoryIntegrated",
          "ActiveDirectoryInteractive",
          "ActiveDirectoryDeviceCodeFlow",
          "ActiveDirectoryManagedIdentity",
          "ActiveDirectoryMSI",
          "ActiveDirectoryDefault"
        }, (IEqualityComparer<string>) StringComparer.OrdinalIgnoreCase).Contains(this.ConnectionStringBuilder["Authentication"].ToString()) || (bool) this.ConnectionStringBuilder["Integrated Security"] || this.ConnectionStringBuilder["User ID"] is string && (this.ConnectionStringBuilder["User ID"] as string).Length != 0;
      }
    }

    public override void Test()
    {
      if (!(this.ConnectionStringBuilder["Data Source"] is string str1) || str1.Length == 0)
        throw new InvalidOperationException(SR.SqlConnectionProperties_MustSpecifyDataSource);
      string str2 = this.ConnectionStringBuilder["Initial Catalog"] as string;
      try
      {
        base.Test();
      }
      catch (SqlException ex)
      {
        if (ex.Number == 4060 && str2 != null && str2.Length > 0)
          throw new InvalidOperationException(SR.SqlConnectionProperties_CannotTestNonExistentDatabase);
        throw;
      }
    }

    protected override PropertyDescriptor DefaultProperty
    {
      get => this.GetProperties(new Attribute[0])["DataSource"];
    }

    protected override string ToTestString()
    {
      bool flag = (bool) this.ConnectionStringBuilder["Pooling"];
      int num = !this.ConnectionStringBuilder.ShouldSerialize("Pooling") ? 1 : 0;
      this.ConnectionStringBuilder["Pooling"] = (object) false;
      string connectionString = this.ConnectionStringBuilder.ConnectionString;
      this.ConnectionStringBuilder["Pooling"] = (object) flag;
      if (num != 0)
        this.ConnectionStringBuilder.Remove("Pooling");
      return connectionString;
    }

    protected override void Inspect(DbConnection connection)
    {
      if (connection.ServerVersion.StartsWith("07", StringComparison.Ordinal))
        throw new NotSupportedException(SR.SqlConnectionProperties_UnsupportedSqlVersion);
    }

    private void LocalReset() => this["Integrated Security"] = (object) true;
  }
}
