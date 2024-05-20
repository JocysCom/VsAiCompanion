using JocysCom.ClassLibrary.Controls;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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
		}

		public UserProfile Item { get => _Item; set => SetProperty(ref _Item, value); }
		UserProfile _Item;

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

		private void This_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				Item = Global.AppSettings.UserProfiles.FirstOrDefault(x => x.ServiceType == ApiServiceType.Azure);
			}
		}
	}
}
