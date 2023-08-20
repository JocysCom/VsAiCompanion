using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for AboutControl.xaml
	/// </summary>
	public partial class AboutControl : UserControl
	{
		public AboutControl()
		{
			InitHelper.InitTimer(this, InitializeComponent);
		}

		private void HyperLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			ControlsHelper.OpenPath(e.Uri.AbsoluteUri);
		}

		private void UserControl_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			var ai = new AssemblyInfo(typeof(MainControl).Assembly);
			ChangeLogTextBox.Text = ClassLibrary.Helper.FindResource<string>("CHANGELOG.md", ai.Assembly);
			AboutProductLabel.Content = string.Format("{0} {1} {2}", ai.Company, ai.Product, ai.Version);
			AboutDescriptionLabel.Text = ai.Description;
			LicenseTextBox.Text = ClassLibrary.Helper.FindResource<string>("LICENSE", ai.Assembly);
			LicenseTabPage.Header = string.Format("{0} {1} License", ai.Product, ai.Version.ToString(2));
			IconExperienceTextBox.Text = ClassLibrary.Helper.FindResource<string>("IconExperience.License.txt", ai.Assembly);
			AxialisIconSetTextBox.Text = ClassLibrary.Helper.FindResource<string>("AxialisIconSet.Licenses.txt", ai.Assembly);
		}
	}
}
