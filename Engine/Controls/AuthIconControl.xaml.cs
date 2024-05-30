using JocysCom.ClassLibrary.Controls;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for AuthIconControl.xaml
	/// </summary>
	public partial class AuthIconControl : UserControl, INotifyPropertyChanged
	{
		public AuthIconControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			UpdateImage();
		}

		public UserProfile Item
		{
			get => _Item;
			set
			{
				if (_Item != null)
				{
					_Item.PropertyChanged -= _Item_PropertyChanged;
				}
				SetProperty(ref _Item, value);
				if (_Item != null)
				{
					_Item.PropertyChanged += _Item_PropertyChanged;
				}
				UpdateImage();
			}
		}

		private void _Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(UserProfile.Image) ||
				e.PropertyName == nameof(UserProfile.IsConsumer))
				UpdateImage();
		}

		public void UpdateImage()
		{
			var noImage = Item?.Image == null;
			DefaultImage.Visibility = noImage ? Visibility.Visible : Visibility.Collapsed;
			MainImage.Visibility = noImage ? Visibility.Collapsed : Visibility.Visible;
			ConsumerImage.Visibility = Item?.IsConsumer == true ? Visibility.Visible : Visibility.Collapsed;
			BusinessImage.Visibility = Item?.IsConsumer == false ? Visibility.Visible : Visibility.Collapsed;
		}

		UserProfile _Item;

		private void This_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				Item = Global.AppSettings.UserProfiles.FirstOrDefault(x => x.ServiceType == ApiServiceType.Azure);
				AdjustEllipse();
			}
		}

		private void ContentPanel_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
		{
			AdjustEllipse();
		}

		private void AdjustEllipse()
		{
			var width = ContentPanel.ActualWidth;
			var height = ContentPanel.ActualHeight;
			if (double.IsNaN(width) || double.IsNaN(height))
				return;
			var ig = ImageGeometry;
			// Calculate the center and the radius for the ellipse
			ig.Center = new System.Windows.Point(width / 2, height / 2);
			ig.RadiusX = width / 2;
			ig.RadiusY = height / 2;
		}

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

		private void This_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			ControlsHelper.EnsureTabItemSelected(Global.MainControl.OptionsPanel.AuthPanel);
		}
	}
}
