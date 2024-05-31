using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Security;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for AiServiceItemControl.xaml
	/// </summary>
	public partial class AiServiceItemControl : UserControl
	{
		public AiServiceItemControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			Global.OnAiModelsUpdated += Global_AiModelsUpdated;
		}

		[Category("Main"), DefaultValue(ItemType.None)]
		public AiService Item
		{
			get => _Item;
			set
			{
				if (ControlsHelper.IsDesignMode(this))
					return;
				lock (_ItemLock)
				{
					var oldItem = _Item;
					// If old item is not null then detach event handlers.
					if (_Item != null)
					{
						ApiSecretKeyPasswordBox.PasswordChanged -= SecretKeyPasswordBox_PasswordChanged;
						ApiOrganizationIdPasswordBox.PasswordChanged -= OrganizationPasswordBox_PasswordChanged;
						_Item.PropertyChanged -= _Item_PropertyChanged;
					}
					_Item = value ?? new AiService();
					// Make sure that even custom AiModel old and new item is available to select.
					AppHelper.UpdateModelCodes(_Item, AiModels, _Item?.DefaultAiModel, oldItem?.DefaultAiModel);
					DataContext = value;
					// New item is bound. Make sure that custom AiModel only for the new item is available to select.
					AppHelper.UpdateModelCodes(_Item, AiModels, _Item?.DefaultAiModel);
					ControlsHelper.SetText(ApiSecretKeyPasswordBox, Item.ApiSecretKey);
					ControlsHelper.SetText(ApiOrganizationIdPasswordBox, Item.ApiOrganizationId);
					ApiSecretKeyPasswordBox.PasswordChanged += SecretKeyPasswordBox_PasswordChanged;
					ApiOrganizationIdPasswordBox.PasswordChanged += OrganizationPasswordBox_PasswordChanged;
					_Item.PropertyChanged += _Item_PropertyChanged;
					UpdateControlVilibility();
				}
			}
		}

		private void _Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(AiService.ServiceType))
				UpdateControlVilibility();
			if (e.PropertyName == nameof(AiService.ApiSecretKey))
				ControlsHelper.SetText(ApiSecretKeyPasswordBox, Item.ApiSecretKey);
			if (e.PropertyName == nameof(AiService.ApiOrganizationId))
				ControlsHelper.SetText(ApiOrganizationIdPasswordBox, Item.ApiOrganizationId);
		}

		void UpdateControlVilibility()
		{
			var visibility = _Item?.ServiceType == ApiServiceType.None || _Item?.ServiceType == ApiServiceType.OpenAI
				? Visibility.Visible
				: Visibility.Collapsed;
			ModelLabel.Visibility = visibility;
			ModelStackPanel.Visibility = visibility;
			IsDefaultServiceLabel.Visibility = visibility;
			ModelFilterLabel.Visibility = visibility;
			ModelFilterTextBox.Visibility = visibility;
			ApiOrganizationIdLabel.Visibility = visibility;
			ApiOrganizationIdPasswordBox.Visibility = visibility;
			IsAzureOpenAiCheckBox.Visibility = visibility;
			ResponseStreamingCheckBox.Visibility = visibility;
		}

		private AiService _Item;
		private readonly object _ItemLock = new object();

		private void SecretKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
			=> Item.ApiSecretKey = ApiSecretKeyPasswordBox.Password;

		private void OrganizationPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
			=> Item.ApiOrganizationId = ApiOrganizationIdPasswordBox.Password;

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
			var aiServices = Global.AppSettings.AiServices.Where(x => x != Item);
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

		private async void AzureVaultValueRefreshButton_Click(object sender, RoutedEventArgs e)
		{
			var credential = await Security.AppSecurityHelper.GetTokenCredential();
			if (credential == null)
				return;
			//var secret = await Security.AppSecurityHelper
			//	.GetSecretFromKeyVault(AzureVaultNameTextBox.Text, AzureVaultSecretNameTextBox.Text, credential);
			//Item.ApiSecretKey = secret?.Value ?? "";
		}

		#region Vault Items

		public SortableBindingList<object> VaultItems1 { get; }
			= new SortableBindingList<object>() { new { Id = (Guid?)null, Name = "" } };

		public SortableBindingList<VaultItem> VaultItems2
			=> Global.AppSettings.VaultItems;

		private async void ApiSecretKeyRefreshButton_Click(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsOnCooldown(sender))
				return;
			var item = await AppSecurityHelper.RefreshVaultItem(Item.ApiSecretKeyVaultItemId);
			if (item != null)
				Item.ApiSecretKey = item.Value;
		}

		private async void ApiOrganizationIdRefreshButton_Click(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsOnCooldown(sender))
				return;
			var item = await AppSecurityHelper.RefreshVaultItem(Item.ApiOrganizationIdVaultItemId);
			if (item != null)
				Item.ApiOrganizationId = item.Value;
		}

		#endregion

	}
}
