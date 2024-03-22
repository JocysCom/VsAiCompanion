#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
  public class OracleConnectionProperties : AdoDotNetConnectionProperties
  {
    public OracleConnectionProperties()
      : base("System.Data.OracleClient")
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

    private void LocalReset() => this["Unicode"] = (object) true;
  }
}
