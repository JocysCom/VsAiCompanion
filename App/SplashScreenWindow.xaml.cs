using System.Windows;
using System.Windows.Input;

namespace JocysCom.VS.AiCompanion
{
	/// <summary>
	/// Interaction logic for SplashScreenWindow.xaml
	/// </summary>
	public partial class SplashScreenWindow : Window
	{
		public SplashScreenWindow()
		{
			InitializeComponent();
		}

		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonDown(e);
			DragMove();
		}

	}
}
