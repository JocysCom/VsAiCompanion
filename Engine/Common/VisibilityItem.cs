using JocysCom.ClassLibrary.ComponentModel;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class VisibilityItem : NotifyPropertyChanged
	{
		public VisibilityItem()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
		}

		/// <summary>
		/// Full path to control.
		/// For example: "OptionsPanel.MainPanel.AlwaysOnToCheckBox"
		/// </summary>
		[XmlAttribute]
		[DefaultValue(null)]
		public string Path { get => _Path; set => SetProperty(ref _Path, value); }
		string _Path;

		[XmlAttribute]
		[DefaultValue(true)]
		public bool IsVisible { get => _IsVisible; set => SetProperty(ref _IsVisible, value); }
		bool _IsVisible;

		[XmlAttribute]
		[DefaultValue(true)]
		public bool IsEnabled { get => _IsEnabled; set => SetProperty(ref _IsEnabled, value); }
		bool _IsEnabled;

		[XmlIgnore, JsonIgnore]
		public FrameworkElement Element { get; set; }

	}
}
