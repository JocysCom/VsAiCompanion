using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Versioning;

namespace Microsoft.Data.ConnectionUI
{

#if NETCOREAPP
	[SupportedOSPlatform("windows")]
#endif

	public class OleDBSqlConnectionProperties : OleDBSpecializedConnectionProperties
	{
		private static bool _sqlNativeClientRegistered;
		private static List<string> _sqlNativeClientProviders = (List<string>)null;
		private static bool _gotSqlNativeClientRegistered;

		public OleDBSqlConnectionProperties()
		  : base("SQLOLEDB")
		{
			LocalReset();
		}

		public override void Reset()
		{
			base.Reset();
			LocalReset();
		}

		public override bool IsComplete
		{
			get
			{
				return base.IsComplete && ConnectionStringBuilder["Data Source"] is string && (ConnectionStringBuilder["Data Source"] as string).Length != 0 && (ConnectionStringBuilder["Integrated Security"] != null && ConnectionStringBuilder["Integrated Security"].ToString().Equals("SSPI", StringComparison.OrdinalIgnoreCase) || ConnectionStringBuilder["User ID"] is string && (ConnectionStringBuilder["User ID"] as string).Length != 0);
			}
		}

		protected override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
		{
			PropertyDescriptorCollection properties = base.GetProperties(attributes);
			if (OleDBSqlConnectionProperties.SqlNativeClientRegistered && properties.Find("Provider", true) is DynamicPropertyDescriptor propertyDescriptor)
			{
				if (!DisableProviderSelection)
					propertyDescriptor.SetIsReadOnly(false);
				propertyDescriptor.SetConverterType(typeof(OleDBSqlConnectionProperties.SqlProviderConverter));
			}
			return properties;
		}

		private void LocalReset() => this["Integrated Security"] = (object)"SSPI";

		public static List<string> SqlNativeClientProviders
		{
			get
			{
				if (OleDBSqlConnectionProperties._sqlNativeClientProviders == null)
				{
					OleDBSqlConnectionProperties._sqlNativeClientProviders = new List<string>();
					foreach (string registeredProvider in OleDBConnectionProperties.GetRegisteredProviders())
					{
						if (registeredProvider.StartsWith("SQLNCLI"))
						{
							int length = registeredProvider.IndexOf(".");
							if (length > 0)
								OleDBSqlConnectionProperties._sqlNativeClientProviders.Add(registeredProvider.Substring(0, length).ToUpperInvariant());
						}
					}
					OleDBSqlConnectionProperties._sqlNativeClientProviders.Sort();
				}
				return OleDBSqlConnectionProperties._sqlNativeClientProviders;
			}
		}

		private static bool SqlNativeClientRegistered
		{
			get
			{
				if (!OleDBSqlConnectionProperties._gotSqlNativeClientRegistered)
				{
					RegistryKey registryKey = (RegistryKey)null;
					try
					{
						OleDBSqlConnectionProperties._sqlNativeClientRegistered = OleDBSqlConnectionProperties.SqlNativeClientProviders.Count > 0;
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
				List<string> values = new List<string>();
				values.Add("SQLOLEDB");
				foreach (string nativeClientProvider in OleDBSqlConnectionProperties.SqlNativeClientProviders)
					values.Add(nativeClientProvider);
				return new TypeConverter.StandardValuesCollection((ICollection)values);
			}
		}
	}
}
