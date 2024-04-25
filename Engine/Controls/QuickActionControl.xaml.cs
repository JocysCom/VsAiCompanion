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
			copy.Created = DateTime.Now;
			copy.Modified = copy.Created;
			// Hide instructions box by default on Tasks.
			copy.ShowInstructions = false;
			Global.InsertItem(copy, ItemType.Task);
			Global.RaiseOnTasksUpdated();
		}

		private void SourceItems_ListChanged(object sender, ListChangedEventArgs e)
		{
			_ = ControlsHelper.BeginInvoke(() =>
			{
				bool refreshGrid =
					e.ListChangedType == ListChangedType.ItemDeleted ||
					e.ListChangedType == ListChangedType.ItemAdded ||
					e.ListChangedType == ListChangedType.ItemMoved;
				if (e.ListChangedType == ListChangedType.ItemChanged)
				{
					if (e.PropertyDescriptor?.Name == nameof(ISettingsListFileItem.ListGroupNameSortKey))
						refreshGrid = true;
				}
				if (refreshGrid)
					_ = Helper.Delay(RefreshDataGrid);
			});
		}

		public void RefreshDataGrid()
		{
			var items = Global.Templates.Items.Cast<ISettingsListFileItem>().OrderBy(x => x.ListGroupNameSortKey).ToList();
			CollectionsHelper.Synchronize(items, FilteredList);
		}

	}
}
