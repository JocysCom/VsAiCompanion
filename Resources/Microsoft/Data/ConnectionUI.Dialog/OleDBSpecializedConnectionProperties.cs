using System;
using System.ComponentModel;
using System.Runtime.Versioning;

namespace Microsoft.Data.ConnectionUI
{

#if NETCOREAPP
	[SupportedOSPlatform("windows")]
#endif

	public class OleDBSpecializedConnectionProperties : OleDBConnectionProperties
	{
		private string _provider;

		public OleDBSpecializedConnectionProperties(string provider)
		{
			_provider = provider;
			LocalReset();
		}

		public override void Reset()
		{
			base.Reset();
			LocalReset();
		}

		protected override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
		{
			bool providerSelection = DisableProviderSelection;
			try
			{
				DisableProviderSelection = true;
				return base.GetProperties(attributes);
			}
			finally
			{
				DisableProviderSelection = providerSelection;
			}
		}

		private void LocalReset() => this["Provider"] = (object)_provider;
	}
}
