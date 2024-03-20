using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for AvatarControl.xaml
	/// </summary>
	public partial class AvatarControl : UserControl
	{
		public AvatarControl()
		{
			InitializeComponent();
			CreateAndBeginAnimation();
		}

		private readonly SolidColorBrush pathColor = new SolidColorBrush(Colors.White);
		private readonly SolidColorBrush elipseColor = new SolidColorBrush(Colors.Yellow);
		Storyboard storyboard = new Storyboard();

		public void CreateAndBeginAnimation()
		{
			// Paths and times.
			CreateAnimatedEllipseOnPath("0:0:2", "0:0:5", "M 376,388 364,376 331,321 282,226 248,156 196,24");
			CreateAnimatedEllipseOnPath("0:0:0", "0:0:5", "M 376,388 364,376 331,321 282,226 248,156 196,24");
			CreateAnimatedEllipseOnPath("0:0:0", "0:0:5", "M 777,446 C 784,440 791,435 797,428 804,420 808,422 818,410 827,398 831,399 832,387 833,375 829,370 838,356 847,342 852,335 856,329 863,318 871,307 877,295 884,283 889,270 896,257 900,249 906,241 909,233 913,223 913,212 918,202 923,192 932,184 938,174 948,155 952,133 962,114 976,87 1011,36 1011,36");

			// Begin animation.
			AvatarCanvas.Visibility = Visibility.Visible;
			storyboard.Begin();
		}

		private void CreateAnimatedEllipseOnPath(string start, string duration, string figure)
		{
			const int size = 30;
			var sizeHalfNegative = -size / 2;
			var figureParsed = PathGeometry.CreateFromGeometry(Geometry.Parse(figure));
			var startParsed = TimeSpan.Parse(start);
			var durationParsed = TimeSpan.Parse(duration);


			// Create elements.
			var grid = new Grid
			{
				Height = size,
				Width = size,
				Margin = new Thickness(sizeHalfNegative, sizeHalfNegative, 0, 0),
				RenderTransform = new MatrixTransform(),
			};

			var ellipse = new Ellipse
			{
				Height = 0,
				Width = 0,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				Fill = elipseColor,
			};

			var path = new Path
			{
				Data = figureParsed,
				Stroke = pathColor,
				StrokeThickness = 1,
			};

			// Add elements.
			grid.Children.Add(ellipse);
			AvatarCanvas.Children.Add(grid);
			AvatarCanvas.Children.Add(path);

			// Create moving animation.
			var matrixAnimation = new MatrixAnimationUsingPath
			{
				PathGeometry = figureParsed,
				BeginTime = startParsed,
				Duration = durationParsed,
				RepeatBehavior = RepeatBehavior.Forever,
			};

			Storyboard.SetTarget(matrixAnimation, grid);
			Storyboard.SetTargetProperty(matrixAnimation, new PropertyPath("(UIElement.RenderTransform).(MatrixTransform.Matrix)"));
			storyboard.Children.Add(matrixAnimation);

			// Create size change animation.
			CreateSizeAnimation(startParsed, durationParsed, ellipse, "Width", size);
			CreateSizeAnimation(startParsed, durationParsed, ellipse, "Height", size);
		}

		private void CreateSizeAnimation(TimeSpan start, TimeSpan duration, UIElement element, string property, double size)
		{
			var path1 = TimeSpan.FromSeconds(0);
			var maxSize1 = TimeSpan.FromSeconds(2);
			var maxSize2 = duration - TimeSpan.FromSeconds(1);
			var path2 = duration;

			var animation = new DoubleAnimationUsingKeyFrames
			{
				RepeatBehavior = RepeatBehavior.Forever,
				BeginTime = start, 
			};

			Storyboard.SetTarget(animation, element);
			Storyboard.SetTargetProperty(animation, new PropertyPath(property));

			animation.KeyFrames.Add(new DiscreteDoubleKeyFrame { KeyTime = path1, Value = 0 });
			animation.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = maxSize1, Value = size });
			animation.KeyFrames.Add(new DiscreteDoubleKeyFrame { KeyTime = maxSize2, Value = size });
			animation.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = path2, Value = 0 });

			storyboard.Children.Add(animation);
		}
	}
}
