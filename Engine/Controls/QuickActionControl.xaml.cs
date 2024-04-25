using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for QuickActionControl.xaml
	/// </summary>
	public partial class QuickActionControl : UserControl, INotifyPropertyChanged
	{
		public QuickActionControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			InitSearch();
			Global.Templates.Items.ListChanged += SourceItems_ListChanged;
		}

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

		private void UserControl_Loaded(object sender, RoutedEventArgs e)
		{
		}

		private void UserControl_Unloaded(object sender, RoutedEventArgs e)
		{
		}

		bool selectionsUpdating = false;
		private void SourceItems_ListChanged(object sender, ListChangedEventArgs e)
		{
			_ = ControlsHelper.BeginInvoke(() =>
			{
				if (e.ListChangedType == ListChangedType.ItemChanged)
				{
					bool refreshGrid = false;
					if (!selectionsUpdating)
					{
						if (e.PropertyDescriptor?.Name == nameof(ISettingsListFileItem.IsPinned))
							refreshGrid = true;
						if (e.PropertyDescriptor?.Name == nameof(ISettingsListFileItem.ListGroupNameSortKey))
							refreshGrid = true;
					}
					if (refreshGrid)
						_ = Helper.Delay(RefreshDataGrid, 500);
				}
			});
		}

		public void RefreshDataGrid()
		{
			var view = (ICollectionView)MyToolBar.ItemsSource;
			view.Refresh();
		}

		#region Search Filter

		private SearchHelper<ISettingsListFileItem> _SearchHelper;

		private void InitSearch()
		{
			_SearchHelper = new SearchHelper<ISettingsListFileItem>((x) =>
			{
				//var s = SearchTextBox.Text;
				var s = "";
				// Item type specific code.
				if (x is TemplateItem ti)
				{
					return string.IsNullOrEmpty(s) ||
						(ti.Name ?? "").IndexOf(s, StringComparison.OrdinalIgnoreCase) > -1 ||
						(ti.Text ?? "").IndexOf(s, StringComparison.OrdinalIgnoreCase) > -1;
				}
				else
				{
					return string.IsNullOrEmpty(s) ||
						(x.Name ?? "").IndexOf(s, StringComparison.OrdinalIgnoreCase) > -1;
				}
			}, null, new ObservableCollection<ISettingsListFileItem>());
			_SearchHelper.SetSource(Global.Templates.Items);
			FilteredList = _SearchHelper.FilteredList;
			OnPropertyChanged(nameof(FilteredList));
		}

		public ObservableCollection<ISettingsListFileItem> FilteredList { get; set; }

		#endregion

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}
}
