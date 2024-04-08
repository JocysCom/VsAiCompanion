using Microsoft.Win32;
using System;
using System.Collections;
using System.ComponentModel;
using System.Runtime.Versioning;

namespace Microsoft.Data.ConnectionUI
{

#if NETCOREAPP
	[SupportedOSPlatform("windows")]
#endif

	public class OleDBAccessConnectionProperties : OleDBSpecializedConnectionProperties
	{
		private static bool _access12ProviderRegistered;
		private static bool _gotAccess12ProviderRegistered;
		private bool _userChangedProvider;

		public OleDBAccessConnectionProperties()
		  : base("Microsoft.Jet.OLEDB.4.0")
		{
			_userChangedProvider = false;
		}

		public override void Reset()
		{
			base.Reset();
			_userChangedProvider = false;
		}

		public override object this[string propertyName]
		{
			set
			{
				base[propertyName] = value;
				if (string.Equals(propertyName, "Provider", StringComparison.OrdinalIgnoreCase))
				{
					if (value != null && value != DBNull.Value)
						OnProviderChanged((object)ConnectionStringBuilder, EventArgs.Empty);
					else
						_userChangedProvider = false;
				}
				if (!string.Equals(propertyName, "Data Source", StringComparison.Ordinal))
					return;
				OnDataSourceChanged((object)ConnectionStringBuilder, EventArgs.Empty);
			}
		}

		public override void Remove(string propertyName)
		{
			base.Remove(propertyName);
			if (string.Equals(propertyName, "Provider", StringComparison.OrdinalIgnoreCase))
				_userChangedProvider = false;
			if (!string.Equals(propertyName, "Data Source", StringComparison.Ordinal))
				return;
			OnDataSourceChanged((object)ConnectionStringBuilder, EventArgs.Empty);
		}

		public override void Reset(string propertyName)
		{
			base.Reset(propertyName);
			if (string.Equals(propertyName, "Provider", StringComparison.OrdinalIgnoreCase))
				_userChangedProvider = false;
			if (!string.Equals(propertyName, "Data Source", StringComparison.Ordinal))
				return;
			OnDataSourceChanged((object)ConnectionStringBuilder, EventArgs.Empty);
		}

		public override bool IsComplete
		{
			get
			{
				return base.IsComplete && ConnectionStringBuilder["Data Source"] is string && (ConnectionStringBuilder["Data Source"] as string).Length != 0;
			}
		}

		public override void Test()
		{
			if (!(ConnectionStringBuilder["Data Source"] is string str) || str.Length == 0)
				throw new InvalidOperationException(SR.GetString("OleDBAccessConnectionProperties_MustSpecifyDataSource"));
			base.Test();
		}

		public override string ToDisplayString()
		{
			string str = (string)null;
			if (ConnectionStringBuilder.ContainsKey("Jet OLEDB:Database Password") && ConnectionStringBuilder.ShouldSerialize("Jet OLEDB:Database Password"))
			{
				str = ConnectionStringBuilder["Jet OLEDB:Database Password"] as string;
				ConnectionStringBuilder.Remove("Jet OLEDB:Database Password");
			}
			string displayString = base.ToDisplayString();
			if (str != null)
				ConnectionStringBuilder["Jet OLEDB:Database Password"] = (object)str;
			return displayString;
		}

		protected override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
		{
			PropertyDescriptorCollection properties1 = base.GetProperties(attributes);
			if (OleDBAccessConnectionProperties.Access12ProviderRegistered)
			{
				if (properties1.Find("Provider", true) is DynamicPropertyDescriptor propertyDescriptor)
				{
					if (!DisableProviderSelection)
						propertyDescriptor.SetIsReadOnly(false);
					propertyDescriptor.SetConverterType(typeof(OleDBAccessConnectionProperties.JetProviderConverter));
					propertyDescriptor.AddValueChanged((object)ConnectionStringBuilder, new EventHandler(OnProviderChanged));
				}
				PropertyDescriptor baseDescriptor = properties1.Find("DataSource", true);
				if (baseDescriptor != null)
				{
					int index = properties1.IndexOf(baseDescriptor);
					PropertyDescriptor[] properties2 = new PropertyDescriptor[properties1.Count];
					properties1.CopyTo((Array)properties2, 0);
					properties2[index] = (PropertyDescriptor)new DynamicPropertyDescriptor(baseDescriptor);
					properties2[index].AddValueChanged((object)ConnectionStringBuilder, new EventHandler(OnDataSourceChanged));
					properties1 = new PropertyDescriptorCollection(properties2, true);
				}
			}
			PropertyDescriptor baseDescriptor1 = properties1.Find("Jet OLEDB:Database Password", true);
			if (baseDescriptor1 != null)
			{
				int index = properties1.IndexOf(baseDescriptor1);
				PropertyDescriptor[] properties3 = new PropertyDescriptor[properties1.Count];
				properties1.CopyTo((Array)properties3, 0);
				properties3[index] = (PropertyDescriptor)new DynamicPropertyDescriptor(baseDescriptor1, new Attribute[1]
				{
		  (Attribute) PasswordPropertyTextAttribute.Yes
				});
				properties1 = new PropertyDescriptorCollection(properties3, true);
			}
			return properties1;
		}

		private static bool Access12ProviderRegistered
		{
			get
			{
				if (!OleDBAccessConnectionProperties._gotAccess12ProviderRegistered)
				{
					RegistryKey registryKey = (RegistryKey)null;
					try
					{
						registryKey = Registry.ClassesRoot.OpenSubKey("Microsoft.ACE.OLEDB.12.0");
						OleDBAccessConnectionProperties._access12ProviderRegistered = registryKey != null;
					}
					finally
					{
						registryKey?.Close();
					}
					OleDBAccessConnectionProperties._gotAccess12ProviderRegistered = true;
				}
				return OleDBAccessConnectionProperties._access12ProviderRegistered;
			}
		}

		private void OnProviderChanged(object sender, EventArgs e)
		{
			if (!OleDBAccessConnectionProperties.Access12ProviderRegistered)
				return;
			_userChangedProvider = true;
		}

		private void OnDataSourceChanged(object sender, EventArgs e)
		{
			if (!OleDBAccessConnectionProperties.Access12ProviderRegistered || _userChangedProvider || !(this["Data Source"] is string str))
				return;
			if (str.Trim().ToUpperInvariant().EndsWith(".ACCDB", StringComparison.Ordinal))
				base["Provider"] = (object)"Microsoft.ACE.OLEDB.12.0";
			else
				base["Provider"] = (object)"Microsoft.Jet.OLEDB.4.0";
		}

		private class JetProviderConverter : StringConverter
		{
			public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;

			public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) => true;

			public override TypeConverter.StandardValuesCollection GetStandardValues(
			  ITypeDescriptorContext context)
			{
				return new TypeConverter.StandardValuesCollection((ICollection)new string[2]
				{
		  "Microsoft.Jet.OLEDB.4.0",
		  "Microsoft.ACE.OLEDB.12.0"
				});
			}
		}
	}
}
