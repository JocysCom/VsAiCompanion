using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
			else if (DataType == ItemType.UiPreset)
			{
				UiPresetItemPanel.Item = (UiPresetItem)item;
			}
			else if (DataType == ItemType.Embeddings)
			{
				EmbeddingItemPanel.Item = (EmbeddingsItem)item;
			}
			else if (DataType == ItemType.MailAccount)
			{
				MailAccountItemPanel.Item = (MailAccount)item;
			}
			else if (DataType == ItemType.VaultItem)
			{
				VaultItemPanel.Item = (Security.VaultItem)item;
			}
			else if (DataType == ItemType.AiService)
			{
				AiServiceItemPanel.Item = (AiService)item;
			}
			else if (DataType == ItemType.AiModel)
			{
				AiModelItemPanel.Item = (AiModel)item;
			}
			ItemPanel.Visibility = currentItem == null
				? Visibility.Collapsed
				: Visibility.Visible;
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


		UserControl ItemPanel;

		public TemplateItemControl TemplateItemPanel;
		FineTuningItemControl FineTuningItemPanel;
		AssistantItemControl AssistantItemPanel;
		public ListsItemControl ListsItemPanel;
		UiPresetItemControl UiPresetItemPanel;
		EmbeddingsItemControl EmbeddingItemPanel;
		MailAccountItemControl MailAccountItemPanel;
		VaultItemControl VaultItemPanel;
		AiServiceItemControl AiServiceItemPanel;
		AiModelItemControl AiModelItemPanel;

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
						// Workaroud with web browser control and non-expanding grid cells.
						ListPanel.NameColumn.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
						control.ChatPanel.MessagesPanel.WebBrowserDataLoaded += (sender, e) =>
						{
							ListPanel.NameColumn.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
							ListPanel.MainDataGrid.UpdateLayout();
						};
						// Workaroud End.
						ConfigureControl(control, value);
						control.DataType = value;
						TemplateItemPanel = control;
					}
				}
				else if (value == ItemType.FineTuning)
				{
					if (FineTuningItemPanel == null)
					{
						var control = new FineTuningItemControl();
						ConfigureControl(control, value);
						control.DataType = value;
						FineTuningItemPanel = control;
					}
				}
				else if (value == ItemType.Assistant)
				{
					if (AssistantItemPanel == null)
					{
						var control = new AssistantItemControl();
						ConfigureControl(control, value);
						control.DataType = value;
						AssistantItemPanel = control;
					}
				}
				else if (value == ItemType.Lists)
				{
					if (ListsItemPanel == null)
					{
						var control = new ListsItemControl();
						ConfigureControl(control, value);
						control.DataType = value;
						ListsItemPanel = control;
					}
				}
				else if (value == ItemType.UiPreset)
				{
					if (UiPresetItemPanel == null)
					{
						var control = new UiPresetItemControl();
						ConfigureControl(control, value);
						control.DataType = value;
						UiPresetItemPanel = control;
					}
				}
				else if (value == ItemType.Embeddings)
				{
					if (EmbeddingItemPanel == null)
					{
						var control = new EmbeddingsItemControl();
						ConfigureControl(control, value);
						control.DataType = value;
						EmbeddingItemPanel = control;
					}
				}
				else if (value == ItemType.MailAccount)
				{
					if (MailAccountItemPanel == null)
					{
						var control = new MailAccountItemControl();
						ConfigureControl(control, value);
						control.DataType = value;
						MailAccountItemPanel = control;
					}
				}
				else if (value == ItemType.VaultItem)
				{
					if (VaultItemPanel == null)
					{
						var control = new VaultItemControl();
						ConfigureControl(control, value);
						control.DataType = value;
						VaultItemPanel = control;
					}
				}
				else if (value == ItemType.AiService)
				{
					if (VaultItemPanel == null)
					{
						var control = new AiServiceItemControl();
						ConfigureControl(control, value);
						control.DataType = value;
						AiServiceItemPanel = control;
					}
				}
				else if (value == ItemType.AiModel)
				{
					if (VaultItemPanel == null)
					{
						var control = new AiModelItemControl();
						ConfigureControl(control, value);
						control.DataType = value;
						AiModelItemPanel = control;
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

		private void MessagesPanel_WebBrowserDataLoaded(object sender, System.EventArgs e)
		{
			throw new System.NotImplementedException();
		}

		private ItemType _DataType;

		void ConfigureControl(UserControl control, ItemType itemType)
		{
			Grid.SetColumn(control, 2);
			MainGrid.Children.Add(control);
			control.Visibility = Visibility.Collapsed;
			control.Name = $"{itemType}Panel";
			ItemPanel = control;
		}

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

		private void MainGridSplitter_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
		{
			// Browser always visible dureing resizing if CTRL key is down.
			var shiftDown =
				System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) ||
				System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift);
			if (shiftDown)
				return;
			var wb = TemplateItemPanel?.ChatPanel?.MessagesPanel?._WebBrowser;
			if (wb != null)
			{
				var bodiesSize = TemplateItemPanel?.Item?.Messages.Sum(x => x.Body?.Length ?? 0);
				var attachmentsSize = TemplateItemPanel?.Item?.Attachments.Sum(x => x.Data?.Length ?? 0);
				var isLarge = (bodiesSize + attachmentsSize) > 1024 * 8;
				if (!settignsStored)
				{
					_BitmapScalingMode = RenderOptions.GetBitmapScalingMode(wb);
					_TextRenderingMode = TextOptions.GetTextRenderingMode(wb);
					// Disable animations to improve resize performance
					_VisualBrush = new VisualBrush(wb);
					_Visual = _VisualBrush.Visual;
					settignsStored = true;
				}
				// Hide content for large pages for faster resizing.
				if (isLarge)
				{
					wb.Visibility = Visibility.Hidden;
					RenderOptions.SetBitmapScalingMode(wb, BitmapScalingMode.LowQuality);
					TextOptions.SetTextRenderingMode(wb, TextRenderingMode.Grayscale);
				}
			}
		}

		bool settignsStored;
		BitmapScalingMode _BitmapScalingMode;
		TextRenderingMode _TextRenderingMode;
		Visual _Visual;
		VisualBrush _VisualBrush;

		private void MainGridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
		{
			var wb = TemplateItemPanel?.ChatPanel?.MessagesPanel?._WebBrowser;
			if (wb != null)
			{
				wb.Visibility = Visibility.Visible;
				RenderOptions.SetBitmapScalingMode(wb, _BitmapScalingMode);
				TextOptions.SetTextRenderingMode(wb, _TextRenderingMode);
				_VisualBrush.Visual = _Visual;
			}
		}
		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
				UiPresetsManager.RemoveControls(ListPanel);
			}
		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}
}
