using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Shared
{
	/// <summary>
	/// Interaction logic for MaskDrawingControl.xaml
	/// </summary>
	public partial class MaskDrawingControl : UserControl
	{
		public MaskDrawingControl()
		{
			InitializeComponent();
			InitializeInkCanvas();
			this.DataContext = this;
		}

		private void InitializeInkCanvas()
		{
			MaskCanvas.DefaultDrawingAttributes = new DrawingAttributes
			{
				Color = Colors.Black,
				Width = BrushSize,
				Height = BrushSize,
				FitToCurve = true,
				IgnorePressure = true,
			};
		}

		public static readonly DependencyProperty BrushSizeProperty =
			DependencyProperty.Register("BrushSize", typeof(double), typeof(MaskDrawingControl), new PropertyMetadata(10.0, OnBrushSizeChanged));

		public double BrushSize
		{
			get { return (double)GetValue(BrushSizeProperty); }
			set { SetValue(BrushSizeProperty, value); }
		}

		private static void OnBrushSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var control = d as MaskDrawingControl;
			if (control.MaskCanvas != null && control.MaskCanvas.DefaultDrawingAttributes != null)
			{
				control.MaskCanvas.DefaultDrawingAttributes.Width = (double)e.NewValue;
				control.MaskCanvas.DefaultDrawingAttributes.Height = (double)e.NewValue;
			}
		}

		private void EraserToggleButton_Checked(object sender, RoutedEventArgs e)
		{
			MaskCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
		}

		private void EraserToggleButton_Unchecked(object sender, RoutedEventArgs e)
		{
			MaskCanvas.EditingMode = InkCanvasEditingMode.Ink;
		}

		public ImageSource BaseImageSource
		{
			get { return BaseImage.Source; }
			set { BaseImage.Source = value; }
		}

		public void ClearMask()
		{
			MaskCanvas.Strokes.Clear();
		}

		public void SaveMaskImage(string maskFilePath)
		{
			var bounds = VisualTreeHelper.GetDescendantBounds(MaskCanvas);
			var dpi = 96d;

			var rtb = new RenderTargetBitmap(
				(int)bounds.Width, (int)bounds.Height,
				dpi, dpi, PixelFormats.Pbgra32);

			var dv = new DrawingVisual();
			using (var dc = dv.RenderOpen())
			{
				var vb = new VisualBrush(MaskCanvas);
				dc.DrawRectangle(vb, null, new Rect(new Point(), bounds.Size));
			}
			rtb.Render(dv);

			// Save the image as PNG
			var encoder = new PngBitmapEncoder();
			encoder.Frames.Add(BitmapFrame.Create(rtb));

			using (var fs = new FileStream(maskFilePath, FileMode.Create, FileAccess.Write))
			{
				encoder.Save(fs);
			}
		}

		public void LoadSelectionsIntoInkCanvas(string jsonString)
		{
			// Assume you have your JSON data as a string.
			//string jsonString = File.ReadAllText("selections.json");
			// Deserialize the JSON string into a list of ObjectSelection instances.
			var selections = JsonConvert.DeserializeObject<List<ObjectSelection>>(jsonString);
			LoadSelections(selections);
		}

		public void LoadSelections(List<ObjectSelection> selections)
		{
			// Iterate through each selection and add it to the InkCanvas.
			foreach (var selection in selections)
			{
				// Convert selection data points to StylusPointCollection.
				StylusPointCollection stylusPoints = new StylusPointCollection();
				foreach (var point in selection.SelectionData)
				{
					stylusPoints.Add(new StylusPoint(point.X, point.Y));
				}

				// Create a stroke with the stylus points.
				Stroke stroke = new Stroke(stylusPoints)
				{
					DrawingAttributes = new DrawingAttributes()
					{
						Color = Colors.Red,       // Set the color of the stroke.
						Width = 2,                // Set the width of the stroke.
						Height = 2
					}
				};

				// Add the stroke to the InkCanvas.
				MaskCanvas.Strokes.Add(stroke);
			}
		}

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			// Set the base image for the MaskDrawingControl
			//var imageUri = new Uri("pack://application:,,,/path_to_your_image.png", UriKind.Absolute);
			//BaseImageSource = new BitmapImage(imageUri);
		}
	}
}
