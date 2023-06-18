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
		}

		private void MainDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
				LoadPositions();
				OnPropertyChanged(nameof(ListPanelVisibility));
			}
		}
		private ItemType _ItemControlType;

		TaskSettings PanelSettings { get; set; } = new TaskSettings();

		private void PanelSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PanelSettings.IsListPanelVisible))
			{
				LoadPositions();
				OnPropertyChanged(nameof(ListPanelVisibility));
			}
		}

		#endregion

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
			if (ItemControlType == ItemType.None)
				return;
			var position = 0.0;
			if (PanelSettings.IsListPanelVisible)
			{
				position = ItemControlType == ItemType.Task
					? Global.AppSettings.TasksGridSplitterPosition
					: Global.AppSettings.TemplatesGridSplitterPosition;
			}
			PositionSettings.SetGridSplitterPosition(MainGrid, position);
		}

		void SavePositions()
		{
			if (ItemControlType == ItemType.None)
				return;
			var position = PositionSettings.GetGridSplitterPosition(MainGrid);
			if (position == 0.0)
				return;
			if (ItemControlType == ItemType.Task)
				Global.AppSettings.TasksGridSplitterPosition = position;
			if (ItemControlType == ItemType.Template)
				Global.AppSettings.TemplatesGridSplitterPosition = position;
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
