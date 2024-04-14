using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for TemplateControl.xaml
	/// </summary>
	public partial class SettingsControl : UserControl, INotifyPropertyChanged
	{

		public SettingsControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			Global.AppSettings.StartPosition.PositionLoaded += StartPosition_PositionLoaded;
			MainGrid.SizeChanged += MainGrid_SizeChanged;
			Global.OnSaveSettings += Global_OnSaveSettings;
		}

		private async void MainDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			await Helper.Delay(UpdateOnSelectionChanged, AppHelper.NavigateDelayMs);
		}

		ISettingsListFileItem currentItem;

		void UpdateOnSelectionChanged()
		{
			var item = ListPanel.MainDataGrid.SelectedItems.Cast<ISettingsListFileItem>().FirstOrDefault();
			if (currentItem != null)
				currentItem.PropertyChanged -= CurrentItem_PropertyChanged;
			currentItem = item;
			if (DataType == ItemType.Task || DataType == ItemType.Template)
			{
				TemplateItemPanel.Item = (TemplateItem)item;
			}
			else if (DataType == ItemType.FineTuning)
			{
				FineTuningItemPanel.Item = (FineTuningItem)item;
			}
			else if (DataType == ItemType.Assistant)
			{
				AssistantItemPanel.Item = (AssistantItem)item;
			}
			else if (DataType == ItemType.Lists)
			{
				ListsItemPanel.Item = (ListInfo)item;
			}
			else if (DataType == ItemType.Embeddings)
			{
				EmbeddingItemPanel.Item = (EmbeddingsItem)item;
			}
			else if (DataType == ItemType.MailAccount)
			{
				MailAccountItemPanel.Item = (MailAccount)item;
			}
			if (currentItem != null)
				currentItem.PropertyChanged += CurrentItem_PropertyChanged;
		}

		private void CurrentItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			var items = ListPanel.MainDataGrid.SelectedItems.Cast<ISettingsListFileItem>().ToList();
			if (items.Count < 2)
				return;
			var text = $"Do you want to apply the same change to all {items.Count} selected items?";
			var caption = $"{Global.Info.Product} - Apply Value - {e.PropertyName}";
			var result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (result != MessageBoxResult.Yes)
				return;
			var pi = currentItem.GetType().GetProperty(e.PropertyName);
			var value = pi.GetValue(currentItem);
			foreach (var item in items)
				pi.SetValue(item, value);
		}

		#region ■ Properties

		public TemplateItemControl TemplateItemPanel;
		FineTuningItemControl FineTuningItemPanel;
		AssistantItemControl AssistantItemPanel;
		ListsItemControl ListsItemPanel;
		EmbeddingsItemControl EmbeddingItemPanel;
		MailAccountItemControl MailAccountItemPanel;

		[Category("Main"), DefaultValue(ItemType.None)]
		public ItemType DataType
		{
			get => _DataType;
			set
			{
				_DataType = value;
				if (ControlsHelper.IsDesignMode(this))
					return;
				if (value == ItemType.Task || value == ItemType.Template)
				{
					if (TemplateItemPanel == null)
					{
						var control = new TemplateItemControl();
						Grid.SetColumn(control, 2);
						MainGrid.Children.Add(control);
						control.DataType = value;
						control.Visibility = Visibility.Visible;
						TemplateItemPanel = control;
					}
				}
				else if (value == ItemType.FineTuning)
				{
					if (FineTuningItemPanel == null)
					{
						var control = new FineTuningItemControl();
						Grid.SetColumn(control, 2);
						MainGrid.Children.Add(control);
						control.DataType = value;
						control.Visibility = Visibility.Visible;
						FineTuningItemPanel = control;
					}
				}
				else if (value == ItemType.Assistant)
				{
					if (AssistantItemPanel == null)
					{
						var control = new AssistantItemControl();
						Grid.SetColumn(control, 2);
						MainGrid.Children.Add(control);
						control.DataType = value;
						control.Visibility = Visibility.Visible;
						AssistantItemPanel = control;
					}
				}
				else if (value == ItemType.Lists)
				{
					if (ListsItemPanel == null)
					{
						var control = new ListsItemControl();
						Grid.SetColumn(control, 2);
						MainGrid.Children.Add(control);
						control.DataType = value;
						control.Visibility = Visibility.Visible;
						ListsItemPanel = control;
					}
				}
				else if (value == ItemType.Embeddings)
				{
					if (EmbeddingItemPanel == null)
					{
						var control = new EmbeddingsItemControl();
						Grid.SetColumn(control, 2);
						MainGrid.Children.Add(control);
						control.DataType = value;
						control.Visibility = Visibility.Visible;
						EmbeddingItemPanel = control;
					}
				}
				else if (value == ItemType.MailAccount)
				{
					if (MailAccountItemPanel == null)
					{
						var control = new MailAccountItemControl();
						Grid.SetColumn(control, 2);
						MainGrid.Children.Add(control);
						control.DataType = value;
						control.Visibility = Visibility.Visible;
						MailAccountItemPanel = control;
					}
				}
				else
					return;
				if (ControlsHelper.IsDesignMode(this))
					return;
				ListPanel.MainDataGrid.SelectionChanged -= MainDataGrid_SelectionChanged;
				// Update panel settings.
				PanelSettings.PropertyChanged -= PanelSettings_PropertyChanged;
				PanelSettings = Global.AppSettings.GetTaskSettings(value);
				PanelSettings.PropertyChanged += PanelSettings_PropertyChanged;
				// Update child control types.
				ListPanel.DataType = value;
				ListPanel.MainDataGrid.SelectionChanged += MainDataGrid_SelectionChanged;
				OnPropertyChanged(nameof(ListPanelVisibility));
			}
		}
		private ItemType _DataType;

		TaskSettings PanelSettings { get; set; } = new TaskSettings();

		private void PanelSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PanelSettings.IsListPanelVisible))
			{
				UpdateGridPosition();
				OnPropertyChanged(nameof(ListPanelVisibility));
			}
		}

		#endregion

		#region GridSplitter Postion

		private bool _LoadGridPosition = true;

		private void Global_OnSaveSettings(object sender, System.EventArgs e)
		{
			SavePositions();
		}

		/// <summary>
		/// This method will be called twice:
		/// 1. At the beginning when the Window is shown, and
		/// 2. when Global.AppSettings.StartPosition.LoadPosition(this) is called in MainWindow.xaml.cs.
		/// </summary>
		private void MainGrid_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (e.WidthChanged && _LoadGridPosition)
				LoadPositions();
		}

		/// <summary>
		/// Runs when windows size and postion is loaded from settings.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void StartPosition_PositionLoaded(object sender, System.EventArgs e)
		{
			LoadPositions();
			// The size of the grid position could remain the same after resizing the parent window.
			// Set _LoadGridPosition to true to ensure that the grid separator position will be loaded
			// as soon as the grid size changes due to the top window size change.
			_LoadGridPosition = true;
		}

		void LoadPositions()
		{
			if (DataType == ItemType.None)
				return;
			lock (MainGrid)
			{
				MainGrid.SizeChanged -= MainGrid_SizeChanged;
				if (UpdateGridPosition())
					_LoadGridPosition = false;
				MainGrid.SizeChanged += MainGrid_SizeChanged;
			}
		}

		bool UpdateGridPosition()
		{
			var position = 0.0;
			if (PanelSettings.IsListPanelVisible)
				position = PanelSettings.GridSplitterPosition;
			return PositionSettings.SetGridSplitterPosition(MainGrid, position, null, true);
		}

		void SavePositions()
		{
			if (DataType == ItemType.None)
				return;
			var position = PositionSettings.GetGridSplitterPosition(MainGrid);
			if (position == null || position == 0.0)
				return;
			PanelSettings.GridSplitterPosition = position.Value;
		}

		#endregion

		public Visibility ListPanelVisibility
			=> PanelSettings.IsListPanelVisible ? Visibility.Visible : Visibility.Collapsed;

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}
}
