using JocysCom.ClassLibrary.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for AiModelBoxControl.xaml
	/// </summary>
	public partial class AiModelBoxControl : UserControl, INotifyPropertyChanged
	{
		public AiModelBoxControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			Global_OnAiServicesUpdated(null, null);
			Global.OnAiModelsUpdated += Global_OnAiModelsUpdated;
			Global.OnAiServicesUpdated += Global_OnAiServicesUpdated;
		}

		private void Global_OnAiServicesUpdated(object sender, EventArgs e)
		{
			AiServicesComboBox.ItemsSource = Global.AppSettings.AiServices
				.Where(x => x.ServiceType == ApiServiceType.None || x.ServiceType == ApiServiceType.OpenAI);
		}

		public IAiServiceModel Item
		{
			get
			{
				return _item;
			}
			set
			{
				if (_item != null)
				{
					AiServicesComboBox.SelectionChanged -= AiServicesComboBox_SelectionChanged;
				}
				_item = value;
				if (_item != null)
				{
					AiServicesComboBox.SelectionChanged += AiServicesComboBox_SelectionChanged;
					var aiServiceId = _item.AiServiceId;
					if (aiServiceId == Guid.Empty)
						aiServiceId = Global.AppSettings.AiServices.FirstOrDefault(x => x.IsDefault)?.Id ??
							Global.AppSettings.AiServices.FirstOrDefault()?.Id ?? Guid.Empty;
					AiServicesComboBox.SelectedValue = aiServiceId;
				}
				OnPropertyChanged(nameof(Item));
			}
		}
		public IAiServiceModel _item;

		public void AiServicesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_item == null)
				return;
			AppHelper.UpdateModelCodes(_item.AiService, AiModels, _item?.AiModel);
		}

		private async void ModelRefreshButton_Click(object sender, RoutedEventArgs e)
		{
			if (_item == null)
				return;
			await AppHelper.UpdateModels(_item.AiService);
		}

		public ObservableCollection<string> AiModels { get; } = new ObservableCollection<string>();

		private void Global_OnAiModelsUpdated(object sender, EventArgs e)
		{
			if (_item == null)
				return;
			// New item is bound. Make sure that custom AiModel only for the new item is available to select.
			AppHelper.UpdateModelCodes(_item.AiService, AiModels, _item?.AiModel);
		}

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
			}
		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


		#endregion

	}
}
