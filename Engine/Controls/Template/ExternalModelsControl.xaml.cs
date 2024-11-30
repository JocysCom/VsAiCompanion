using JocysCom.ClassLibrary.Controls;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Template
{
	/// <summary>
	/// Interaction logic for ExternalModelsControl.xaml
	/// </summary>
	public partial class ExternalModelsControl : UserControl, INotifyPropertyChanged
	{
		public ExternalModelsControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			var debugVisibility = InitHelper.IsDebug
				? Visibility.Visible
				: Visibility.Collapsed;
			UseAudioToTextCheckBox.Visibility = debugVisibility;
			TemplateAudioToTextComboBox.Visibility = debugVisibility;
			Global.Templates.Items.ListChanged += Global_Templates_Items_ListChanged;
		}

		public Dictionary<string, string> PluginTemplates
			=> Global.Templates.Items.ToDictionary(x => x.Name, x => x.Name);

		public Dictionary<string, string> GenerateTitleTemplates
		{
			get
			{
				var kv = Global.Templates.Items
					.Where(x => x.Name.StartsWith(SettingsSourceManager.TemplateGenerateTitleTaskName))
					.ToDictionary(x => x.Name, x => x.Name);
				//kv.Add(null, "Default");
				return kv;
			}
			set { }
		}


		private void Global_Templates_Items_ListChanged(object sender, ListChangedEventArgs e)
		{
			AppHelper.CollectionChanged(e, () =>
			{
				OnPropertyChanged(nameof(GenerateTitleTemplates));
				OnPropertyChanged(nameof(PluginTemplates));
			});
		}

		/// <summary>
		/// Gets or sets the data item associated with this control.
		/// </summary>
		public TemplateItem Item
		{
			get => _Item;
			set
			{
				if (Equals(value, _Item))
					return;
				_Item = value;
				OnPropertyChanged(nameof(Item));
			}
		}
		private TemplateItem _Item;

		/// <summary>
		/// Handles the Loaded event of the user control.
		/// Initializes help content and UI presets when the control is loaded.
		/// </summary>
		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
			}
		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion
	}
}
