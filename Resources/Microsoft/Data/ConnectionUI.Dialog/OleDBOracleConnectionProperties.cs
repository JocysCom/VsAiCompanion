using System.Runtime.Versioning;

namespace Microsoft.Data.ConnectionUI
{

#if NETCOREAPP
	[SupportedOSPlatform("windows")]
#endif

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
				return base.IsComplete && ConnectionStringBuilder["Data Source"] is string && (ConnectionStringBuilder["Data Source"] as string).Length != 0 && ConnectionStringBuilder["User ID"] is string && (ConnectionStringBuilder["User ID"] as string).Length != 0;
			}
		}
	}
}
