using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Security;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for VaultItemControl.xaml
	/// </summary>
	public partial class VaultItemControl : UserControl, INotifyPropertyChanged
	{
		public VaultItemControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
		}

		#region List Panel Item

		TaskSettings PanelSettings { get; set; } = new TaskSettings();

		[Category("Main"), DefaultValue(ItemType.None)]
		public ItemType DataType
		{
			get => _DataType;
			set
			{
				_DataType = value;
				if (ControlsHelper.IsDesignMode(this))
					return;
				// Update panel settings.
				PanelSettings.PropertyChanged -= PanelSettings_PropertyChanged;
				PanelSettings = Global.AppSettings.GetTaskSettings(value);
				PanelSettings.PropertyChanged += PanelSettings_PropertyChanged;
				PanelSettings.UpdateListToggleButtonIcon(ListToggleButton);
			}
		}
		private ItemType _DataType;

		private async void PanelSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			await Task.Delay(0);
		}

		private void ListToggleButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			PanelSettings.UpdateListToggleButtonIcon(ListToggleButton, true);
		}

		#endregion


		public VaultItem Item
		{
			get => _Item;
			set
			{
				if (_Item != null)
				{
					_Item.PropertyChanged -= _Item_PropertyChanged;
				}
				_Item = value;
				if (_Item != null)
				{
					_Item.PropertyChanged += _Item_PropertyChanged;
				}
				OnPropertyChanged(nameof(Item));
			}
		}
		VaultItem _Item;

		private void _Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(EmbeddingsItem.Source))
			{
			}
		}

		/// <summary>
		/// Stores cancellation tokens created on this control that can be stopped with the [Stop] button.
		/// </summary>
		ObservableCollection<CancellationTokenSource> cancellationTokenSources = new ObservableCollection<CancellationTokenSource>();

		private async void RefreshButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			var exception = await AppHelper.ExecuteMethod(
				cancellationTokenSources,
				async (cancellationToken) =>
			{
				var secret = await AppSecurityHelper.GetSecretFromKeyVault(Item.VaultName, Item.VaultItemName);
				Item.Value = secret?.Value;
				ValueTextBox.Password = Item.Value;
				Item.ActivationDate = secret?.Properties?.ExpiresOn;
				Item.ExpirationDate = secret?.Properties?.NotBefore;

			});
		}

		private void CopyButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			var value = ValueTextBox.Password;
			if (!string.IsNullOrEmpty(value))
				System.Windows.Clipboard.SetText(value);
		}

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

	}
}
