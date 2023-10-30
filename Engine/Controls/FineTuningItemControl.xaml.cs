using JocysCom.ClassLibrary.Controls;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for FineTuningControl.xaml
	/// </summary>
	public partial class FineTuningItemControl : UserControl
	{
		public FineTuningItemControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
		}

		public FineTuningItem Item
		{
			get => _Item;
			set
			{
				_Item = value;
				DataContext = value;
				AiModelBoxPanel.BindData(value);
				IconPanel.BindData(value);
				FineTuningSourceDataPanel.Data = value;
				FineTuningTuningDataPanel.Data = value;
				FineTuningRemoteDataPanel.Data = value;
				FineTuningJobListPanel.Data = value;
				FineTuningModelListPanel.Data = value;
				if (value != null)
				{
					var path = Global.GetPath(value);
					var di = new DirectoryInfo(path);
					DataFolderTextBox.Text = di.FullName;
					if (!di.Exists)
						di.Create();
				}
			}
		}
		FineTuningItem _Item;

		[Category("Main"), DefaultValue(ItemType.None)]
		public ItemType DataType
		{
			get => _DataType;
			set
			{
				_DataType = value;
				// Update panel settings.
				PanelSettings.PropertyChanged -= PanelSettings_PropertyChanged;
				PanelSettings = Global.AppSettings.GetTaskSettings(value);
				PanelSettings.PropertyChanged += PanelSettings_PropertyChanged;
				// Update the rest.
				UpdateBarToggleButtonIcon();
				UpdateListToggleButtonIcon();
				OnPropertyChanged(nameof(BarPanelVisibility));
				OnPropertyChanged(nameof(TemplateItemVisibility));
			}
		}
		private ItemType _DataType;

		private void OpenButton_Click(object sender, System.Windows.RoutedEventArgs e)
			=> ControlsHelper.OpenUrl(DataFolderTextBox.Text);

		private void ConvertToJsonlButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			LogTextBox.Clear();
		}

		private void ConvertToJsonButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{

		}

		#region PanelSettings

		TaskSettings PanelSettings { get; set; } = new TaskSettings();

		private void PanelSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PanelSettings.IsBarPanelVisible))
			{
				OnPropertyChanged(nameof(BarPanelVisibility));
				OnPropertyChanged(nameof(TemplateItemVisibility));
				UpdateBarToggleButtonIcon();
			}
		}

		private void ListToggleButton_Click(object sender, RoutedEventArgs e)
		{
			PanelSettings.IsListPanelVisible = !PanelSettings.IsListPanelVisible;
			UpdateListToggleButtonIcon();
		}

		public void UpdateListToggleButtonIcon()
		{
			var rt = new RotateTransform();
			rt.Angle = PanelSettings.IsListPanelVisible ? 0 : 180;
			ListToggleButton.RenderTransform = rt;
			ListToggleButton.RenderTransformOrigin = new Point(0.5, 0.5);
		}

		public Visibility BarPanelVisibility
			=> PanelSettings.IsBarPanelVisible ? Visibility.Visible : Visibility.Collapsed;

		public Visibility TemplateItemVisibility
			=> PanelSettings.IsBarPanelVisible && _DataType == ItemType.Template ? Visibility.Visible : Visibility.Collapsed;

		private void BarToggleButton_Click(object sender, RoutedEventArgs e)
		{
			PanelSettings.IsBarPanelVisible = !PanelSettings.IsBarPanelVisible;
			UpdateListToggleButtonIcon();
		}

		public void UpdateBarToggleButtonIcon()
		{
			var rt = new RotateTransform();
			rt.Angle = PanelSettings.IsBarPanelVisible ? 90 : 270;
			BarToggleButton.RenderTransform = rt;
			BarToggleButton.RenderTransformOrigin = new Point(0.5, 0.5);
		}

		#endregion

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}
}
