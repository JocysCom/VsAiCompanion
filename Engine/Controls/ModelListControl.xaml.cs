using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for ProjectsListControl.xaml
	/// </summary>
	public partial class ModelListControl : UserControl
	{
		public ModelListControl()
		{
			InitializeComponent();
			//ScanProgressPanel.Visibility = Visibility.Collapsed;
			if (ControlsHelper.IsDesignMode(this))
				return;
			var item = Global.FineTunes.Items.FirstOrDefault();
			Item = item;
			MainDataGrid.ItemsSource = CurrentItems;
			UpdateButtons();
		}

		FineTune Item { get; set; }

		public SortableBindingList<model> CurrentItems { get; set; } = new SortableBindingList<model>();

		public void SelectByName(string name)
		{
			var list = new List<string>() { name };
			ControlsHelper.RestoreSelection(MainDataGrid, nameof(TemplateItem.Name), list, 0);
		}

		public void ShowColumns(params DataGridColumn[] args)
		{
			var all = MainDataGrid.Columns.ToArray();
			foreach (var control in all)
				control.Visibility = args.Contains(control) ? Visibility.Visible : Visibility.Collapsed;
		}

		public void ShowButtons(params Button[] args)
		{
			var all = new Button[] { DeleteButton };
			foreach (var control in all)
				control.Visibility = args.Contains(control) ? Visibility.Visible : Visibility.Collapsed;
		}

		private async void MainDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			await Helper.Delay(UpdateButtons, AppHelper.NavigateDelayMs);
		}

		void UpdateButtons()
		{
			var selecetedItems = MainDataGrid.SelectedItems.Cast<model>();
			var isSelected = selecetedItems.Count() > 0;
			//var isBusy = (Global.MainControl?.InfoPanel?.Tasks?.Count ?? 0) > 0;
			DeleteButton.IsEnabled = isSelected;
		}

		private void UserControl_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
		}

		public void InsertItem(model item)
		{
			//var position = FindInsertPosition(CurrentItems, item);
			// Make sure new item will be selected and focused.
			//CurrentItems.Insert(position, item);
		}

		private void DeleteButton_Click(object sender, RoutedEventArgs e)
		{
			var items = MainDataGrid.SelectedItems.Cast<model>().ToList();
			if (items.Count == 0)
				return;
			var names = string.Join("\r\n", items.Select(x=>x.id));
			var text = $"Do you want to delete {items.Count} item{(items.Count > 1 ? "s" : "")}?";
			text += "\r\n\r\n";
			text += names;
			var caption = $"{Global.Info.Product} - Delete";
			var result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (result != MessageBoxResult.Yes)
				return;
			// Use begin invoke or grid update will deadlock on same thread.
			ControlsHelper.BeginInvoke(async () =>
			{
				var client = new Client(Item.AiService);
				var deleted = false;
				foreach (var item in items)
				{
					var response = await client.DeleteModelAsync(item.id);
					deleted |= response.deleted;
				}
				if (deleted)
					await Refresh();
			});
		}

		/// <summary>
		///  Event is fired when the DataGrid is rendered and its items are loaded,
		///  which means that you can safely select items at this point.
		/// </summary>
		private void MainDataGrid_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
		}

		public async Task Refresh()
		{
			var client = new Client(Item.AiService);
			var response = await client.GetModelsAsync();
			var items = response.First()?.data.Where(x=>x.id.StartsWith("ft:")).ToArray();
			CollectionsHelper.Synchronize(items, CurrentItems);
		}

		private async void RefreshButton_Click(object sender, RoutedEventArgs e)
		{
			await Refresh();
		}
	}

}

