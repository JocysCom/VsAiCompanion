using JocysCom.ClassLibrary.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
			ButtonsGrid.Visibility = Visibility.Collapsed;
			UpdateButtons();
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
			if (e.PropertyName == nameof(UserProfile.Image))
				UpdateImage();
		}

		public void UpdateImage()
		{
			var noImage = Item?.Image == null;
			DefaultImage.Visibility = noImage ? Visibility.Visible : Visibility.Collapsed;
			MainImage.Visibility = noImage ? Visibility.Collapsed : Visibility.Visible;
			ConsumerImage.Visibility = Visibility.Collapsed;
			BusinessImage.Visibility = Visibility.Collapsed;
			//ConsumerImage.Visibility = Item?.IsConsumer == true ? Visibility.Visible : Visibility.Collapsed;
			//BusinessImage.Visibility = Item?.IsConsumer == false ? Visibility.Visible : Visibility.Collapsed;
		}

		UserProfile _Item;

		private void This_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				Global.UserProfile.PropertyChanged += Profile_PropertyChanged;
				Item = Global.UserProfile;
				AdjustEllipse();
				AppHelper.InitHelp(this);
				// Hide/Show is controlled by app settings.
				//UiPresetsManager.AddControls(this);
				//UiPresetsManager.InitControl(this, true);
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
			ControlsHelper.EnsureTabItemSelected(Global.MainControl.OptionsPanel.MicrosoftAccountsPanel);
		}

		private void SignButton_Click(object sender, RoutedEventArgs e)
		{

		}

		private void Profile_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(UserProfile.IsSignedIn))
				UpdateButtons();
		}

		public void UpdateButtons()
		{
			var profile = Global.UserProfile;
			SignInButton.Visibility = profile.IsSignedIn
				? Visibility.Collapsed : Visibility.Visible;
			SignOutButton.Visibility = !profile.IsSignedIn
				? Visibility.Collapsed : Visibility.Visible;
		}

		private void Grid_MouseEnter(object sender, MouseEventArgs e)
		{
			ButtonsGrid.Visibility = Visibility.Visible;
		}

		private void Grid_MouseLeave(object sender, MouseEventArgs e)
		{
			ButtonsGrid.Visibility = Visibility.Collapsed;
		}

		private async void SignInButton_Click(object sender, RoutedEventArgs e)
		{
			await Global.MainControl.OptionsPanel.MicrosoftAccountsPanel.SignIn();
		}

		private async void SignOuButton_Click(object sender, RoutedEventArgs e)
		{
			await Global.MainControl.OptionsPanel.MicrosoftAccountsPanel.SignOut();
		}
	}
}
