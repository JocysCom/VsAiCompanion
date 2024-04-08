using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.IO;
using System.Runtime.Versioning;

namespace Microsoft.Data.ConnectionUI
{

#if NETCOREAPP
	[SupportedOSPlatform("windows")]
#endif
	public class SqlFileConnectionProperties : SqlConnectionProperties
	{
		private string _defaultDataSource;

		public SqlFileConnectionProperties()
		  : this((string)null)
		{
		}

		public SqlFileConnectionProperties(string defaultInstanceName)
		{
			_defaultDataSource = ".";
			if (defaultInstanceName != null && defaultInstanceName.Length > 0)
			{
				SqlFileConnectionProperties connectionProperties = this;
				connectionProperties._defaultDataSource = connectionProperties._defaultDataSource + "\\" + defaultInstanceName;
			}
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
				return base.IsComplete && ConnectionStringBuilder["AttachDbFilename"] is string && (ConnectionStringBuilder["AttachDbFilename"] as string).Length != 0;
			}
		}

		public override void Test()
		{
			string path = ConnectionStringBuilder["AttachDbFilename"] as string;
			try
			{
				ConnectionStringBuilder["AttachDbFilename"] = path != null && path.Length != 0 ? (object)Path.GetFullPath(path) : throw new InvalidOperationException(SR.GetString("SqlFileConnectionProperties_NoFileSpecified"));
				if (!File.Exists(ConnectionStringBuilder["AttachDbFilename"] as string))
					throw new InvalidOperationException(SR.GetString("SqlFileConnectionProperties_CannotTestNonExistentMdf"));
				base.Test();
			}
			catch (SqlException ex)
			{
				if (ex.Number == -2)
					throw new ApplicationException(ex.Errors[0].Message + Environment.NewLine + SR.GetString("SqlFileConnectionProperties_TimeoutReasons"));
				throw;
			}
			finally
			{
				if (path != null && path.Length > 0)
					ConnectionStringBuilder["AttachDbFilename"] = (object)path;
			}
		}

		protected override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
		{
			PropertyDescriptorCollection properties1 = base.GetProperties(attributes);
			PropertyDescriptor baseDescriptor = properties1.Find("DataSource", true);
			if (baseDescriptor != null)
			{
				int index = properties1.IndexOf(baseDescriptor);
				PropertyDescriptor[] properties2 = new PropertyDescriptor[properties1.Count];
				properties1.CopyTo((Array)properties2, 0);
				properties2[index] = (PropertyDescriptor)new DynamicPropertyDescriptor(baseDescriptor, new Attribute[1]
				{
		  (Attribute) new TypeConverterAttribute(typeof (SqlFileConnectionProperties.DataSourceConverter))
				});
				(properties2[index] as DynamicPropertyDescriptor).CanResetValueHandler = new CanResetValueHandler(CanResetDataSource);
				(properties2[index] as DynamicPropertyDescriptor).ResetValueHandler = new ResetValueHandler(ResetDataSource);
				properties1 = new PropertyDescriptorCollection(properties2, true);
			}
			return properties1;
		}

		private void LocalReset()
		{
			this["Data Source"] = (object)_defaultDataSource;
			this["User Instance"] = (object)true;
			this["Connection Timeout"] = (object)30;
		}

		private bool CanResetDataSource(object component)
		{
			return !(this["Data Source"] is string) || !(this["Data Source"] as string).Equals(_defaultDataSource, StringComparison.OrdinalIgnoreCase);
		}

		private void ResetDataSource(object component)
		{
			this["Data Source"] = (object)_defaultDataSource;
		}

		private class DataSourceConverter : StringConverter
		{
			private TypeConverter.StandardValuesCollection _standardValues;

			public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;

			public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) => true;

			public override TypeConverter.StandardValuesCollection GetStandardValues(
			  ITypeDescriptorContext context)
			{
				if (_standardValues == null)
				{
					string[] values = (string[])null;
					if (HelpUtils.IsWow64())
					{
						List<string> stringList = new List<string>();
						stringList.AddRange((IEnumerable<string>)HelpUtils.GetValueNamesWow64("SOFTWARE\\Microsoft\\Microsoft SQL Server\\Instance Names\\SQL", 257));
						stringList.AddRange((IEnumerable<string>)HelpUtils.GetValueNamesWow64("SOFTWARE\\Microsoft\\Microsoft SQL Server\\Instance Names\\SQL", 513));
						values = stringList.ToArray();
					}
					else
					{
						RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Microsoft SQL Server\\Instance Names\\SQL");
						if (registryKey != null)
						{
							using (registryKey)
								values = registryKey.GetValueNames();
						}
					}
					if (values != null)
					{
						for (int index = 0; index < values.Length; ++index)
							values[index] = !string.Equals(values[index], "MSSQLSERVER", StringComparison.OrdinalIgnoreCase) ? ".\\" + values[index] : ".";
						_standardValues = new TypeConverter.StandardValuesCollection((ICollection)values);
					}
					else
						_standardValues = new TypeConverter.StandardValuesCollection((ICollection)new string[0]);
				}
				return _standardValues;
			}
		}
	}
}
