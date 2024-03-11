using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace JocysCom.ClassLibrary.Configuration
{
	public class SettingsFileItem : SettingsItem, ISettingsFileItem
	{
		[DefaultValue(null)]
		public string Path { get => _Path; set => SetProperty(ref _Path, value); }
		string _Path;

		[DefaultValue(null)]
		public string Name { get => _Name; set => SetProperty(ref _Name, value); }
		string _Name;

		[XmlIgnore]
		string ISettingsFileItem.BaseName { get => Name; set => Name = value; }

		[XmlIgnore]
		DateTime ISettingsFileItem.WriteTime { get; set; }

		#region ■ INotifyPropertyChanged

		protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			base.OnPropertyChanged(propertyName);
			((ISettingsFileItem)this).WriteTime = DateTime.Now;
		}

		#endregion

	}
}
