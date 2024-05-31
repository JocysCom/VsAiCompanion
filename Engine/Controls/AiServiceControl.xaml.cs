using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for AiServiceControl.xaml
	/// </summary>
	public partial class AiServiceControl : UserControl
	{
		public AiServiceControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			ListPanel.MainDataGrid.SelectionChanged += ListPanel_MainDataGrid_SelectionChanged;
			_ = Helper.Delay(UpdateOnSelectionChanged, AppHelper.NavigateDelayMs);
		}

		void UpdateOnSelectionChanged()
		{
			var item = ListPanel.MainDataGrid.SelectedItems.Cast<AiService>().FirstOrDefault();
			ItemPanel.Item = item;
			ItemPanel.Visibility = item == null
					? Visibility.Collapsed
					: Visibility.Visible;
		}

		private async void ListPanel_MainDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			await Helper.Delay(UpdateOnSelectionChanged, AppHelper.NavigateDelayMs);
		}

		TaskSettings PanelSettings { get; set; }

		#region GridSplitter Postion

		private bool _gridSplitterPositionSet;

		private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
		{
			SavePositions();
		}

		private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
		{

			if (e.WidthChanged && !_gridSplitterPositionSet)
			{
				_gridSplitterPositionSet = true;
				LoadPositions();
			}
		}

		void LoadPositions()
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			var position = PanelSettings.GridSplitterPosition;
			PositionSettings.SetGridSplitterPosition(MainGrid, position, null, true);
		}

		void SavePositions()
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			var position = PositionSettings.GetGridSplitterPosition(MainGrid);
			if (position == null || position == 0.0)
				return;
			PanelSettings.GridSplitterPosition = position.Value;
		}

		#endregion

		private void ItemPanel_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			PanelSettings = Global.AppSettings.GetTaskSettings(ItemType.AiService);
		}
	}
}
