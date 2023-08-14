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
		}

		private void ListPanel_MainDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var item = ListPanel.MainDataGrid.SelectedItems.Cast<AiService>().FirstOrDefault();
			ItemPanel.Item = item;
		}

		TaskSettings PanelSettings { get; set; } = new TaskSettings();

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
			var position = Global.AppSettings.AiServiceData.GridSplitterPosition;
			PositionSettings.SetGridSplitterPosition(MainGrid, position);
		}

		void SavePositions()
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			var position = PositionSettings.GetGridSplitterPosition(MainGrid);
			if (position == 0.0)
				return;
			Global.AppSettings.AiServiceData.GridSplitterPosition = position;
		}

		#endregion

	}
}
