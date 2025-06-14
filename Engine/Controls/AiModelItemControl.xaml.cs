using JocysCom.ClassLibrary.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for AiModelItemControl.xaml
	/// </summary>
	public partial class AiModelItemControl : UserControl, INotifyPropertyChanged
	{
		public AiModelItemControl()
		{
			DataContext = new AiModel();
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
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


		[Category("Main"), DefaultValue(ItemType.None)]
		public AiModel Item
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
						_Item.PropertyChanged -= _Item_PropertyChanged;
					}
					_Item = value ?? new AiModel();
					DataContext = _Item;
					_Item.PropertyChanged += _Item_PropertyChanged;
					UpdateControlVilibility();
				}
				OnPropertyChanged(nameof(Item));
			}
		}
		private AiModel _Item;
		private readonly object _ItemLock = new object();

		private void _Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
		}


		public ObservableCollection<CheckBoxViewModel> FeaturesItemsSource
		{
			get
			{
				if (_FeaturesItemsSource == null)
					_FeaturesItemsSource = EnumComboBox.GetItemSource<AiModelFeatures>();
				return _FeaturesItemsSource;
			}
			set => _FeaturesItemsSource = value;
		}
		ObservableCollection<CheckBoxViewModel> _FeaturesItemsSource;


		public AiModelEndpointType[] EndpointTypeItemsSource { get; set; } =
			(AiModelEndpointType[])Enum.GetValues(typeof(AiModelEndpointType));

		void UpdateControlVilibility()
		{
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
