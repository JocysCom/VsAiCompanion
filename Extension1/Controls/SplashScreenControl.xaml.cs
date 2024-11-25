using Microsoft.VisualStudio.RpcContracts.RemoteUI;
using System.Windows;
using System.Windows.Media.Animation;

namespace JocysCom.VS.AiCompanion.Extension.Controls
{
	/// <summary>
	/// Interaction logic for SplashScreenControl.xaml
	/// </summary>
	public partial class SplashScreenControl : System.Windows.Controls.UserControl, IRemoteUserControl
	{
		public SplashScreenControl()
		{
			InitializeComponent();
			// Create the animation for the Opacity property
			DoubleAnimation opacityAnimation = new DoubleAnimation
			{
				From = 0.5,
				To = 0.2,
				Duration = new Duration(TimeSpan.FromSeconds(1.0)),
				AutoReverse = true,
				RepeatBehavior = RepeatBehavior.Forever
			};
			// Begin the animation on the LoadingTextBlock
			//LoadingDotsTextBlock.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
		}

		public void Dispose()
		{
		}

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			// Retrieve the storyboard from the resources and start the animation
			Storyboard loadingDotsStoryboard = (Storyboard)Resources["LoadingDotsStoryboard"];
			loadingDotsStoryboard.Begin();
		}
	}
}
