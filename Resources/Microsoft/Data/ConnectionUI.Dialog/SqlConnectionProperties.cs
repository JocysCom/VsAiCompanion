using System;
using System.ComponentModel;
using System.Data.Common;
using System.Data.SqlClient;

namespace Microsoft.Data.ConnectionUI
{
  public class SqlConnectionProperties : AdoDotNetConnectionProperties
  {
    private const int SqlError_CannotOpenDatabase = 4060;

    public SqlConnectionProperties()
      : base("System.Data.SqlClient")
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
        return this.ConnectionStringBuilder["Data Source"] is string && (this.ConnectionStringBuilder["Data Source"] as string).Length != 0 && ((bool) this.ConnectionStringBuilder["Integrated Security"] || this.ConnectionStringBuilder["User ID"] is string && (this.ConnectionStringBuilder["User ID"] as string).Length != 0);
      }
    }

    public override void Test()
    {
      if (!(this.ConnectionStringBuilder["Data Source"] is string str1) || str1.Length == 0)
        throw new InvalidOperationException(SR.GetString("SqlConnectionProperties_MustSpecifyDataSource"));
      string str2 = this.ConnectionStringBuilder["Initial Catalog"] as string;
      try
      {
        base.Test();
      }
      catch (SqlException ex)
      {
        if (ex.Number == 4060 && str2 != null && str2.Length > 0)
          throw new InvalidOperationException(SR.GetString("SqlConnectionProperties_CannotTestNonExistentDatabase"));
        throw;
      }
    }

    protected override PropertyDescriptor DefaultProperty
    {
      get => this.GetProperties(new Attribute[0])["DataSource"];
    }

    protected override string ToTestString()
    {
      bool flag1 = (bool) this.ConnectionStringBuilder["Pooling"];
      bool flag2 = !this.ConnectionStringBuilder.ShouldSerialize("Pooling");
      this.ConnectionStringBuilder["Pooling"] = (object) false;
      string connectionString = this.ConnectionStringBuilder.ConnectionString;
      this.ConnectionStringBuilder["Pooling"] = (object) flag1;
      if (flag2)
        this.ConnectionStringBuilder.Remove("Pooling");
      return connectionString;
    }

    protected override void Inspect(DbConnection connection)
    {
      if (connection.ServerVersion.StartsWith("07", StringComparison.Ordinal) || connection.ServerVersion.StartsWith("08", StringComparison.Ordinal))
        throw new NotSupportedException(SR.GetString("SqlConnectionProperties_UnsupportedSqlVersion"));
    }

    private void LocalReset() => this["Integrated Security"] = (object) true;
  }
}
