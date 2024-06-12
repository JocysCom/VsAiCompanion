using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

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

		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonDown(e);
			DragMove();
		}

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			// Retrieve the storyboard from the resources and start the animation
			Storyboard loadingDotsStoryboard = (Storyboard)Resources["LoadingDotsStoryboard"];
			loadingDotsStoryboard.Begin();
		}
	}
}
