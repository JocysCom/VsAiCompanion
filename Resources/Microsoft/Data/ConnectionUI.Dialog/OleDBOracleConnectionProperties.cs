#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
  public class OleDBOracleConnectionProperties : OleDBSpecializedConnectionProperties
  {
    public OleDBOracleConnectionProperties()
      : base("MSDAORA")
    {
    }

    public override bool IsComplete
    {
      get
      {
        return base.IsComplete && this.ConnectionStringBuilder["Data Source"] is string && (this.ConnectionStringBuilder["Data Source"] as string).Length != 0 && this.ConnectionStringBuilder["User ID"] is string && (this.ConnectionStringBuilder["User ID"] as string).Length != 0;
      }
    }
  }
}
