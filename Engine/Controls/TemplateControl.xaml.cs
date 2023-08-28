using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
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
	public partial class TemplateControl : UserControl, INotifyPropertyChanged
	{

		public TemplateControl()
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

		void UpdateOnSelectionChanged()
		{
			var item = ListPanel.MainDataGrid.SelectedItems.Cast<TemplateItem>().FirstOrDefault();
			ItemPanel.BindData(item);
		}


		#region ■ Properties

		[Category("Main"), DefaultValue(ItemType.None)]
		public ItemType ItemControlType
		{
			get => _ItemControlType;
			set
			{
				_ItemControlType = value;
				ListPanel.MainDataGrid.SelectionChanged -= MainDataGrid_SelectionChanged;
				// Update panel settings.
				PanelSettings.PropertyChanged -= PanelSettings_PropertyChanged;
				PanelSettings = Global.AppSettings.GetTaskSettings(value);
				PanelSettings.PropertyChanged += PanelSettings_PropertyChanged;
				// Update child control types.
				ListPanel.ItemControlType = ItemControlType;
				ItemPanel.ItemControlType = ItemControlType;
				ListPanel.MainDataGrid.SelectionChanged += MainDataGrid_SelectionChanged;
				OnPropertyChanged(nameof(ListPanelVisibility));
			}
		}
		private ItemType _ItemControlType;

		TaskSettings PanelSettings { get; set; } = new TaskSettings();

		private void PanelSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PanelSettings.IsListPanelVisible))
				OnPropertyChanged(nameof(ListPanelVisibility));
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
		}

		void LoadPositions()
		{
			if (ItemControlType == ItemType.None)
				return;
			lock (MainGrid)
			{
				MainGrid.SizeChanged -= MainGrid_SizeChanged;
				var position = 0.0;
				if (PanelSettings.IsListPanelVisible)
					position = PanelSettings.GridSplitterPosition;
				var success = PositionSettings.SetGridSplitterPosition(MainGrid, position, null, true);
				if (success)
					_LoadGridPosition = false;
				MainGrid.SizeChanged += MainGrid_SizeChanged;
			}
		}

		void SavePositions()
		{
			if (ItemControlType == ItemType.None)
				return;
			var position = PositionSettings.GetGridSplitterPosition(MainGrid);
			if (position == 0.0)
				return;
			PanelSettings.GridSplitterPosition = position;
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
