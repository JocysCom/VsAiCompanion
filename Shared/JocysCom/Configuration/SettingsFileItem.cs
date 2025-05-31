using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace JocysCom.ClassLibrary.Configuration
{
	/// <summary>Represents a file-based settings entry implementing ISettingsFileItem: stores Path, Name, and IsReadOnlyFile, and updates WriteTime on property changes.</summary>
	public class SettingsFileItem : SettingsItem, ISettingsFileItem
	{
		[DefaultValue(null)]
		public string Path { get => _Path; set => SetProperty(ref _Path, value); }
		string _Path;

		[DefaultValue(null)]
		public string Name { get => _Name; set => SetProperty(ref _Name, value); }
		string _Name;

		[XmlIgnore, JsonIgnore, DefaultValue(null)]
		public bool IsReadOnlyFile { get => _IsReadOnlyFile; set => SetProperty(ref _IsReadOnlyFile, value); }
		bool _IsReadOnlyFile;

		[XmlIgnore, JsonIgnore]
		string ISettingsFileItem.BaseName { get => Name; set => Name = value; }

		[XmlIgnore, JsonIgnore]
		DateTime ISettingsFileItem.WriteTime { get; set; }

		#region ■ INotifyPropertyChanged

		/// <summary>Sets WriteTime to DateTime.Now after a property change.</summary>
		protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			base.OnPropertyChanged(propertyName);
			((ISettingsFileItem)this).WriteTime = DateTime.Now;
		}

		#endregion

	}
}
