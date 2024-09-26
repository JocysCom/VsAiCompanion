using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for QuickActionControl.xaml
	/// </summary>
	public partial class QuickActionControl : UserControl
	{
		public QuickActionControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			Global.Templates.Items.ListChanged += SourceItems_ListChanged;
			_ = Helper.Delay(RefreshDataGrid);
		}

		public ObservableCollection<ISettingsListFileItem> FilteredList { get; set; } = new ObservableCollection<ISettingsListFileItem>();

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			var item = (TemplateItem)((Button)sender).DataContext;
			var copy = item.Copy(true);
			copy.IsPinned = false;
			copy.Created = DateTime.Now;
			copy.Modified = copy.Created;
			Global.InsertItem(copy, ItemType.Task);
			// Select new task in the tasks list on the [Tasks] tab.
			var settings = Global.AppSettings.GetTaskSettings(ItemType.Task);
			//settings.ListSelection = selection;
			settings.Focus = true;
			Global.RaiseOnTasksUpdated();
		}

		private void SourceItems_ListChanged(object sender, ListChangedEventArgs e)
		{
			ControlsHelper.AppBeginInvoke(() =>
			{
				AppHelper.CollectionChanged(e, RefreshDataGrid, nameof(ISettingsListFileItem.ListGroupNameSortKey));
			});
		}

		public void RefreshDataGrid()
		{
			var items = Global.Templates.Items.Cast<ISettingsListFileItem>().OrderBy(x => x.ListGroupNameSortKey).ToList();
			CollectionsHelper.Synchronize(items, FilteredList);
			AppHelper.InitHelp(this);
			UiPresetsManager.InitControl(this, true);
		}

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
			}
		}
	}
}
