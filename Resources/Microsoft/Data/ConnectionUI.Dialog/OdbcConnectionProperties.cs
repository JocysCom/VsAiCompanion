#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
  public class OdbcConnectionProperties : AdoDotNetConnectionProperties
  {
    public OdbcConnectionProperties()
      : base("System.Data.Odbc")
    {
    }

    public override bool IsComplete
    {
      get
      {
        return this.ConnectionStringBuilder["DSN"] is string && (this.ConnectionStringBuilder["DSN"] as string).Length != 0 || this.ConnectionStringBuilder["DRIVER"] is string && (this.ConnectionStringBuilder["DRIVER"] as string).Length != 0;
      }
    }
  }
}
