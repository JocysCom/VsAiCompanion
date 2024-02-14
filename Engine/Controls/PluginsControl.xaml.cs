using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Controls;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for PluginsControl.xaml
	/// </summary>
	public partial class PluginsControl : UserControl
	{
		public PluginsControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
			{
				var list = new SortableBindingList<PluginItem>();
				CurrentItems = list;
				return;
			}
			CurrentItems = Global.AppSettings.Plugins;
			MainDataGrid.ItemsSource = CurrentItems;
		}

		public SortableBindingList<PluginItem> CurrentItems { get; set; }

	}
}
