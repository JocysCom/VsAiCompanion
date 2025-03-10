using JocysCom.ClassLibrary.Controls;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for AiServiceItemControl.xaml
	/// </summary>
	public partial class AiServiceItemControl : UserControl, INotifyPropertyChanged
	{
		public AiServiceItemControl()
		{
			DataContext = new AiService();
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			Global.OnAiModelsUpdated += Global_AiModelsUpdated;
		}


		[Category("Main"), DefaultValue(ItemType.None)]
		public ItemType DataType
		{
			get => _DataType;
			set
			{
				_DataType = value;
				if (ControlsHelper.IsDesignMode(this))
					return;
			}
		}
		private ItemType _DataType;

		/// <summary>
		/// Gets the data item associated with this control.
		/// </summary>
		[Category("Main"), DefaultValue(ItemType.None)]
		public AiService Item
		{
			get => _Item;
		}
		AiService _Item;

		public async Task BindData(AiService value)
		{
			await Task.Delay(0);
			if (ControlsHelper.IsDesignMode(this))
				return;
			var oldItem = _Item;
			// If old item is not null then detach event handlers.
			if (_Item != null)
			{
				_Item.PropertyChanged -= _Item_PropertyChanged;
			}
			if (value != null)
			{
				var assembly = GetType().Assembly;
				var bytes = ClassLibrary.Helper.FindResource<byte[]>($"AI Service - {value.Name}.rtf", assembly);
				//var bytes = AppHelper.ExtractFile("Documents.zip", "Feature - AI Avatar.rtf");
				if (bytes == null)
				{
					MainTabControl.Visibility = Visibility.Collapsed;
				}
				else
				{
					ControlsHelper.SetTextFromResource(HelpRichTextBox, bytes);
					MainTabControl.Visibility = Visibility.Visible;
				}
			}
			_Item = value ?? new AiService();
			// Make sure that even custom AiModel old and new item is available to select.
			AppHelper.UpdateModelCodes(_Item, AiModels, _Item?.DefaultAiModel, oldItem?.DefaultAiModel);
			DataContext = _Item;
			// New item is bound. Make sure that custom AiModel only for the new item is available to select.
			AppHelper.UpdateModelCodes(_Item, AiModels, _Item?.DefaultAiModel);
			_Item.PropertyChanged += _Item_PropertyChanged;
			UpdateControlVilibility();
			//await ExternalLinksPanel.BindData(_Item?.ExternalLinks);
			// Force VaultItemValueControl to update its DataContext
			ApiSecretKeyVaultItemValuePanel.DataContext = _Item;
			ApiOrganizationIdVaultItemValuePanel.DataContext = _Item;
			// Ensure Value is updated
			ApiSecretKeyVaultItemValuePanel.Value = _Item.ApiSecretKey;
			ApiOrganizationIdVaultItemValuePanel.Value = _Item.ApiOrganizationId;
			// Ensure VaultItemId is updated
			ApiSecretKeyVaultItemValuePanel.VaultItemId = _Item.ApiSecretKeyVaultItemId;
			ApiOrganizationIdVaultItemValuePanel.VaultItemId = _Item.ApiOrganizationIdVaultItemId;
		}

		private void _Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(AiService.ServiceType))
				UpdateControlVilibility();
		}

		void UpdateControlVilibility()
		{
			var visibility = _Item?.ServiceType == ApiServiceType.None
				|| _Item?.ServiceType == ApiServiceType.OpenAI
				? Visibility.Visible
				: Visibility.Collapsed;
			ModelLabel.Visibility = visibility;
			ModelStackPanel.Visibility = visibility;
			IsDefaultServiceLabel.Visibility = visibility;
			ModelFilterLabel.Visibility = visibility;
			ModelFilterTextBox.Visibility = visibility;
			ApiOrganizationIdLabel.Visibility = visibility;
			ApiOrganizationIdVaultItemValuePanel.ValuePasswordBox.Visibility = visibility;
			UseAzureOpenAiClientCheckBox.Visibility = visibility;
			ResponseStreamingCheckBox.Visibility = visibility;
		}

		private async void ModelRefreshButton_Click(object sender, RoutedEventArgs e)
		{
			await AppHelper.UpdateModels(Item);
		}

		public BindingList<string> AiModels { get; set; } = new BindingList<string>();

		public ApiServiceType[] ServiceTypes { get; set; } =
			(ApiServiceType[])Enum.GetValues(typeof(ApiServiceType));

		private void Global_AiModelsUpdated(object sender, EventArgs e)
		{
			// New item is bound. Make sure that custom AiModel only for the new item is available to select.
			AppHelper.UpdateModelCodes(Item, AiModels);
			if (string.IsNullOrEmpty(Item.DefaultAiModel) && AiModels?.Count > 0)
				Item.DefaultAiModel = AiModels.FirstOrDefault();
		}

		private void CheckBox_Checked(object sender, RoutedEventArgs e)
		{
			// Remove default flag from all other items.
			var aiServices = Global.AiServices.Items.Where(x => x != Item);
			foreach (var item in aiServices)
			{
				if (item.IsDefault)
					item.IsDefault = false;
			}
		}

		private string previousText = "";

		private void OpenAiBaseUrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			Uri result;
			if (Uri.TryCreate(OpenAiBaseUrlTextBox.Text, UriKind.Absolute, out result))
			{
				var isOK = result.Scheme == Uri.UriSchemeHttps
					|| result.Host.Equals("localhost")
					|| result.Host.Equals("127.0.0.1");
				if (isOK)
					Global.MainControl.InfoPanel.Reset();
				else
					Global.SetWithTimeout(MessageBoxImage.Error, $"OpenAI's base URL for '{_Item?.Name}' is not local and does not use HTTPS!");
				// Check if the current text contains "microsoft.com" or "azure.com"
				// and ensure these were not present in the previous text
				var currentText = OpenAiBaseUrlTextBox.Text;
				bool containsMicrosoft = currentText.Contains("microsoft.com") && !previousText.Contains("microsoft.com");
				bool containsAzure = currentText.Contains("azure.com") && !previousText.Contains("azure.com");
				// Enable IsAzureOpenAI
				if (containsMicrosoft || containsAzure)
					Item.IsAzureOpenAI = true;
				previousText = currentText;
			}

		}

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this);
			}
		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}
}
