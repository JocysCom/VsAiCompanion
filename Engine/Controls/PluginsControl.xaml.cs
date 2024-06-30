using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Controls;
using System.Collections.Generic;
using System.Linq;
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
			var list = new SortableBindingList<PluginCategory>();
			CurrentItems = list;
			MainTabControl.ItemsSource = CurrentItems;
			if (ControlsHelper.IsDesignMode(this))
				return;
			Global.AppSettings.Plugins.ListChanged += Plugins_ListChanged;
			UpdateOnListChanged();
		}

		private async void Plugins_ListChanged(object sender, System.ComponentModel.ListChangedEventArgs e)
		{
			await Helper.Delay(UpdateOnListChanged, AppHelper.NavigateDelayMs);
		}

		private void UpdateOnListChanged()
		{
			var categories = Global.AppSettings.Plugins
				.Select(x => x.Mi.DeclaringType)
				.Distinct()
				.Select(x => new PluginCategory(x))
				.ToList();
			ClassLibrary.Collections.CollectionsHelper.Synchronize(categories, CurrentItems, new PluginCategoryComparer());
		}

		class PluginCategoryComparer : IEqualityComparer<PluginCategory>
		{
			public bool Equals(PluginCategory x, PluginCategory y) => x.Id == y.Id;
			public int GetHashCode(PluginCategory obj) => throw new System.NotImplementedException();
		}

		public SortableBindingList<PluginCategory> CurrentItems { get; set; }

		private void This_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
			}
		}
	}
}
