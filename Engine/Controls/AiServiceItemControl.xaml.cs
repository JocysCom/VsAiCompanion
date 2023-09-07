using JocysCom.ClassLibrary.Controls;
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
			Global.AiModelsUpdated += Global_AiModelsUpdated;
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
						SecretKeyPasswordBox.PasswordChanged -= SecretKeyPasswordBox_PasswordChanged;
						OrganizationPasswordBox.PasswordChanged -= OrganizationPasswordBox_PasswordChanged;
					}
					_Item = value ?? new AiService();
					// Make sure that even custom AiModel old and new item is available to select.
					AppHelper.UpdateModelCodes(_Item, AiModels, _Item?.DefaultAiModel, oldItem?.DefaultAiModel);
					DataContext = value;
					// New item is bound. Make sure that custom AiModel only for the new item is available to select.
					AppHelper.UpdateModelCodes(_Item, AiModels, _Item?.DefaultAiModel);
					SecretKeyPasswordBox.Password = Item.ApiSecretKey;
					SecretKeyPasswordBox.PasswordChanged += SecretKeyPasswordBox_PasswordChanged;
					OrganizationPasswordBox.Password = Item.ApiOrganizationId;
					OrganizationPasswordBox.PasswordChanged += OrganizationPasswordBox_PasswordChanged;
				}
			}
		}
		private AiService _Item;
		private object _ItemLock = new object();

		private void SecretKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
			=> Item.ApiSecretKey = SecretKeyPasswordBox.Password;

		private void OrganizationPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
			=> Item.ApiOrganizationId = OrganizationPasswordBox.Password;

		private async void ModelRefreshButton_Click(object sender, RoutedEventArgs e)
		{
			await AppHelper.UpdateModelsFromAPI(Item);
		}

		public BindingList<string> AiModels { get; set; } = new BindingList<string>();

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
					Global.MainControl.InfoPanel.SetWithTimeout(MessageBoxImage.Error, $"OpenAI's base URL for '{_Item?.Name}' is not local and does not use HTTPS!");
			}

		}
	}
}
