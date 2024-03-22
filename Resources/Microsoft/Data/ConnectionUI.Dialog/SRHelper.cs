using System.Runtime.CompilerServices;

#nullable disable
namespace Microsoft.SqlServer.Management.ConnectionUI
{
	[CompilerGenerated]
	internal class SRHelper
	{
		public static string DataConnectionDialog_DataSourceWithShortProvider(
		  string source,
		  string provider)
		{
			return string.Format(SR.DataConnectionDialog_DataSourceWithShortProvider, source, provider);
		}

		public static string DataConnectionDialog_NoDataProvidersForDataSource(string source)
		{
			return string.Format(SR.DataConnectionDialog_NoDataProvidersForDataSource, source);
		}
	}
}
