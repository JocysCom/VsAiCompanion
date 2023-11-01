using JocysCom.ClassLibrary.Controls;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for AiModelBoxControl.xaml
	/// </summary>
	public partial class AiModelBoxControl : UserControl
	{
		public AiModelBoxControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			AiCompanionComboBox.ItemsSource = Global.AppSettings.AiServices;
			Global.AiModelsUpdated += Global_AiModelsUpdated;
		}

		public IAiServiceModel _item;

		public void BindData(IAiServiceModel item)
		{
			if (item == null)
			{
				AiCompanionComboBox.SelectionChanged -= AiCompanionComboBox_SelectionChanged;
			}
			_item = item;
			DataContext = item;
			if (item != null)
			{
				AiCompanionComboBox.SelectionChanged += AiCompanionComboBox_SelectionChanged;
				var aiServiceId = _item.AiServiceId;
				if (aiServiceId == Guid.Empty)
					aiServiceId = Global.AppSettings.AiServices.FirstOrDefault(x => x.IsDefault)?.Id ??
						Global.AppSettings.AiServices.FirstOrDefault()?.Id ?? Guid.Empty;
				AiCompanionComboBox.SelectedValue = aiServiceId;
			}
		}

		public void AiCompanionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_item == null)
				return;
				AppHelper.UpdateModelCodes(_item.AiService, AiModels, _item?.AiModel);
		}

		private async void ModelRefreshButton_Click(object sender, RoutedEventArgs e)
		{
			if (_item == null)
				return;
			await AppHelper.UpdateModelsFromAPI(_item.AiService);
		}

		public BindingList<string> AiModels { get; set; } = new BindingList<string>();

		private void Global_AiModelsUpdated(object sender, EventArgs e)
		{
			if (_item == null)
				return;
			// New item is bound. Make sure that custom AiModel only for the new item is available to select.
			AppHelper.UpdateModelCodes(_item.AiService, AiModels, _item?.AiModel);
		}

	}
}
