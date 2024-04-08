using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.OleDb;
using System.Runtime.Versioning;

namespace Microsoft.Data.ConnectionUI
{

#if NETCOREAPP
	[SupportedOSPlatform("windows")]
#endif

	public class OleDBConnectionProperties : AdoDotNetConnectionProperties
	{
		private bool _disableProviderSelection;

		public OleDBConnectionProperties()
		  : base("System.Data.OleDb")
		{
		}

		public bool DisableProviderSelection
		{
			get => _disableProviderSelection;
			set => _disableProviderSelection = value;
		}

		public override bool IsComplete
		{
			get
			{
				return ConnectionStringBuilder["Provider"] is string && (ConnectionStringBuilder["Provider"] as string).Length != 0;
			}
		}

		protected override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
		{
			PropertyDescriptorCollection properties1 = base.GetProperties(attributes);
			if (_disableProviderSelection)
			{
				PropertyDescriptor baseDescriptor = properties1.Find("Provider", true);
				if (baseDescriptor != null)
				{
					int index = properties1.IndexOf(baseDescriptor);
					PropertyDescriptor[] properties2 = new PropertyDescriptor[properties1.Count];
					properties1.CopyTo((Array)properties2, 0);
					properties2[index] = (PropertyDescriptor)new DynamicPropertyDescriptor(baseDescriptor, new Attribute[1]
					{
			(Attribute) ReadOnlyAttribute.Yes
					});
					(properties2[index] as DynamicPropertyDescriptor).CanResetValueHandler = new CanResetValueHandler(CanResetProvider);
					properties1 = new PropertyDescriptorCollection(properties2, true);
				}
			}
			return properties1;
		}

		public static List<string> GetRegisteredProviders()
		{
			OleDbDataReader enumerator = OleDbEnumerator.GetEnumerator(Type.GetTypeFromCLSID(NativeMethods.CLSID_OLEDB_ENUMERATOR));
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			using (enumerator)
			{
				while (enumerator.Read())
				{
					switch ((int)enumerator["SOURCES_TYPE"])
					{
						case 1:
						case 3:
							dictionary[enumerator["SOURCES_CLSID"] as string] = (string)null;
							continue;
						default:
							continue;
					}
				}
			}
			List<string> registeredProviders = new List<string>(dictionary.Count);
			RegistryKey registryKey1 = Registry.ClassesRoot.OpenSubKey("CLSID");
			using (registryKey1)
			{
				foreach (KeyValuePair<string, string> keyValuePair in dictionary)
				{
					RegistryKey registryKey2 = registryKey1.OpenSubKey(keyValuePair.Key + "\\ProgID");
					if (registryKey2 != null)
					{
						using (registryKey2)
							registeredProviders.Add(registryKey2.GetValue((string)null) as string);
					}
				}
			}
			registeredProviders.Sort();
			while (registeredProviders.Contains("MSDASQL.1"))
				registeredProviders.Remove("MSDASQL.1");
			return registeredProviders;
		}

		private bool CanResetProvider(object component) => false;
	}
}
