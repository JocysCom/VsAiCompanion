using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Security;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for VaultItemValueControl.xaml
	/// </summary>
	public partial class VaultItemValueControl : UserControl, INotifyPropertyChanged
	{
		public VaultItemValueControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			PropertyChanged += This_PropertyChanged;
			DataContextChanged += This_DataContextChanged;
		}

		private void This_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(VaultItemId))
				UpdateItem();
		}

		private void This_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			UpdateItem();
		}

		public SortableBindingList<object> VaultItems1 { get; }
			= new SortableBindingList<object>() { new { Id = (Guid?)null, Name = "" } };

		public SortableBindingList<VaultItem> VaultItems2 { get; set; }

		private async void VaultItemRefreshButton_Click(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsOnCooldown(sender))
				return;
			_ = await AppSecurityHelper.RefreshVaultItem(Item?.Id);
		}

		private async void AzureVaultValueRefreshButton_Click(object sender, RoutedEventArgs e)
		{
			var credential = await Security.AppSecurityHelper.GetTokenCredential();
			if (credential == null)
				return;
		}

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
					ControlsHelper.SetText(VaultItemValuePasswordBox, Item.Value);
				}
				var useVaultItem = value != null;
				ValuePasswordBox.Visibility = !useVaultItem
					? Visibility.Visible
					: Visibility.Collapsed;
				VaultItemValuePasswordBox.Visibility = useVaultItem
					? Visibility.Visible
					: Visibility.Collapsed;
			}
		}
		VaultItem _Item;

		private void _Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(VaultItem.Value))
				ControlsHelper.SetText(VaultItemValuePasswordBox, Item.Value);
		}

		#region Properties

		/// <summary>Vault Item ID dependency property.</summary>
		public static readonly DependencyProperty VaultItemIdProperty =
			DependencyProperty.Register(nameof(VaultItemId), typeof(Guid?), typeof(VaultItemValueControl));

		public Guid? VaultItemId
		{
			get { return (Guid?)GetValue(VaultItemIdProperty); }
			set
			{
				SetValue(VaultItemIdProperty, value);
			}
		}

		/// <summary>Value dependency property.</summary>
		public static readonly DependencyProperty ValueProperty =
			DependencyProperty.Register(nameof(Value), typeof(string), typeof(VaultItemValueControl));

		public string Value
		{
			get { return (string)GetValue(ValueProperty); }
			set
			{
				SetValue(ValueProperty, value);
				ControlsHelper.SetText(ValuePasswordBox, value);
			}
		}

		#endregion

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		protected void SetProperty<T>(ref T property, T value, [CallerMemberName] string propertyName = null)
		{
			if (Equals(property, value))
				return;
			property = value;
			// Invoke overridden OnPropertyChanged method in the most derived class of the object.
			OnPropertyChanged(propertyName);
		}

		#endregion

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (ControlsHelper.AllowLoad(this))
			{
				VaultItems2 = Global.AppSettings.VaultItems;
				OnPropertyChanged(nameof(VaultItems2));
				UpdateItem();
				ValuePasswordBox.PasswordChanged += ValuePasswordBox_PasswordChanged;
			}
		}

		private void ValuePasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
		{
			if (Value != ValuePasswordBox.Password)
				Value = ValuePasswordBox.Password;
		}

		void UpdateItem()
		{
			Item = Global.AppSettings.VaultItems.FirstOrDefault(x => x.Id == VaultItemId);
		}
	}
}
