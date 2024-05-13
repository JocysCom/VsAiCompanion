using JocysCom.ClassLibrary;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;

namespace JocysCom.VS.AiCompanion.Plugins.Core
{

	/// <summary>
	/// Capture Screen, Window and Region screenshots.
	/// </summary>
	public class ScreenshotHelper
	{
		/// <summary>
		/// Capture screen image
		/// </summary>
		/// <param name="screenId"></param>
		/// <param name="imageFolder"></param>
		/// <param name="format"></param>
		/// <returns></returns>
		/// <exception cref="Exception"></exception>
		public async static Task<OperationResult<string>> CaptureScreen(int? screenId = null, string imageFolder = null, ImageFormat format = null)
		{
			try
			{
				if (screenId == null)
					return await CaptureUserDefinedRegion(imageFolder, format);
				format = format ?? ImageFormat.Png;
				// Determine the folder to save the image, defaulting to the temp directory if not provided
				string folderPath = imageFolder ?? Path.GetTempPath();
				string fileName = $"Capture_{DateTime.Now:yyyyMMddHHmmss}.{format.ToString().ToLower()}";
				string fullPath = Path.Combine(folderPath, fileName);
				if (screenId.HasValue)
				{
					// Capture specific screen
					var screen = Screen.AllScreens.ElementAtOrDefault(screenId.Value);
					if (screen == null)
						return new OperationResult<string>(new ArgumentException("Invalid screenId."));
					CaptureAndSave(screen.Bounds, fullPath, format);
				}
				else
				{
					// Capture all screens by creating a bitmap spanning all screens
					Rectangle totalSize = Screen.AllScreens.Aggregate(Rectangle.Empty, (current, s) => Rectangle.Union(current, s.Bounds));
					CaptureAndSave(totalSize, fullPath, format);
				}
				return new OperationResult<string>(fullPath);
			}
			catch (Exception ex)
			{
				return new OperationResult<string>(ex);
			}
		}

		/// <summary>
		/// Capture Window image.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="imageFolder"></param>
		/// <param name="format"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public async static Task<OperationResult<string>> CaptureWindow(string name = null, string imageFolder = null, ImageFormat format = null)
		{
			if (string.IsNullOrWhiteSpace(name))
				return await CaptureUserDefinedRegion(imageFolder, format);
			format = format ?? ImageFormat.Png;
			var windowHandle = FindWindow(null, name);
			if (windowHandle == IntPtr.Zero)
				return new OperationResult<string>(new ArgumentException($"No window found with the title: {name}"));
			GetWindowRect(windowHandle, out Rectangle windowRect);
			string fullPath = PrepareFilePath(imageFolder, format);
			CaptureAndSave(windowRect, fullPath, format);
			return new OperationResult<string>(fullPath);
		}

		/// <summary>
		/// Capture screen region.
		/// </summary>
		/// <param name="region"></param>
		/// <param name="imageFolder"></param>
		/// <param name="format"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentNullException"></exception>
		public async static Task<OperationResult<string>> CaptureRegion(Rectangle? region = null, string imageFolder = null, ImageFormat format = null)
		{
			if (region == null)
				return await CaptureUserDefinedRegion(imageFolder, format);
			format = format ?? ImageFormat.Png;
			string fullPath = PrepareFilePath(imageFolder, format);
			CaptureAndSave(region.Value, fullPath, format);
			return new OperationResult<string>(fullPath);
		}

		#region Capture Region

		[DllImport("user32.dll")]
		private static extern IntPtr WindowFromPoint(System.Drawing.Point pnt);

		private static SemaphoreSlim _captureRegionSemaphore = new SemaphoreSlim(0, 1);
		private static Rectangle? _selectedRegion;
		private static bool _cancelledByUser = false;

		/// <summary>
		/// Capture user defined region image.
		/// </summary>
		/// <param name="imageFolder"></param>
		/// <param name="format"></param>
		/// <returns></returns>
		public async static Task<OperationResult<string>> CaptureUserDefinedRegion(string imageFolder = null, ImageFormat format = null)
		{
			var results = await GetCaptureRegion();
			if (!results.Success)
				return new OperationResult<string>(new Exception(results.Errors[0]));
			var imageResults = await CaptureRegion(results.Result, imageFolder, format);
			return imageResults;
		}

		/// <summary>
		/// Ask user to define screenshot capturing region.
		/// </summary>
		/// <returns>Image capturing region.</returns>
		public async static Task<OperationResult<Rectangle>> GetCaptureRegion()
		{
			ShowCaptureOverlay();
			// Wait here until signal from the overlay window
			await _captureRegionSemaphore.WaitAsync();
			if (_cancelledByUser)
				return new OperationResult<Rectangle>(new Exception("User cancelled operation with Escape key."));
			if (_selectedRegion == null)
				return new OperationResult<Rectangle>(new Exception("Failed to capture a valid region."));
			return new OperationResult<Rectangle>(_selectedRegion.Value);
		}


		private static System.Windows.Point startPoint;
		private static System.Windows.Shapes.Rectangle selectionRectangle = null;
		private static Canvas canvas = null;
		private static Window overlayWindow = null;

		private static System.Windows.Shapes.Rectangle[] overlayRectangles = null;

		/// <summary>
		/// ShowCaptureOverlay method with modifications for semaphore signaling and escape key handling.
		/// </summary>
		public static void ShowCaptureOverlay()
		{
			overlayWindow = new Window
			{
				WindowStyle = WindowStyle.None,
				AllowsTransparency = true,
				Background = System.Windows.Media.Brushes.Transparent,
				Topmost = true,
				Left = 0,
				Top = 0,
				Width = SystemParameters.VirtualScreenWidth,
				Height = SystemParameters.VirtualScreenHeight,
			};

			canvas = new Canvas
			{
				Background = System.Windows.Media.Brushes.Transparent,
				Width = SystemParameters.VirtualScreenWidth,
				Height = SystemParameters.VirtualScreenHeight
			};
			overlayWindow.Content = canvas;

			selectionRectangle = new System.Windows.Shapes.Rectangle
			{
				Stroke = System.Windows.Media.Brushes.Blue,
				StrokeThickness = 2,
				Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0)),
				StrokeDashArray = new DoubleCollection { 2, 2 }
			};

			// Create 4 rectangles, which will cover sides of selection rectangle.
			// 1st - covers virtual screen above selectionRectangle.
			// 2nd - covers virtual screen below selectionRectangle.
			// 3rd - covers virtual screen on the left of selectionRectangle.
			// 4th - covers virtual screen on the right of selectionRectangle.
			overlayRectangles = Enumerable.Range(0, 5).Select(x =>
			{
				var rect = new System.Windows.Shapes.Rectangle
				{
					Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(64, 0, 0, 0)),
					Width = SystemParameters.VirtualScreenWidth,
					Height = SystemParameters.VirtualScreenHeight,
				};
				Canvas.SetLeft(rect, 0);
				Canvas.SetTop(rect, 0);
				canvas.Children.Add(rect);
				return rect;
			}).ToArray();


			overlayWindow.KeyDown += (sender, e) =>
			{
				if (e.Key == Key.Escape)
				{
					_cancelledByUser = true;
					ReleaseResources();
				}
			};

			overlayWindow.MouseDown += OverlayWindow_MouseDown;
			overlayWindow.MouseMove += OverlayWindow_MouseMove;
			overlayWindow.MouseUp += OverlayWindow_MouseUp;

			// Show and then focus the overlay window to ensure it's topmost and receives user input.
			overlayWindow.Show();
			overlayWindow.Focus();
		}


		private static void ReleaseResources()
		{
			selectionRectangle.Visibility = Visibility.Hidden;
			canvas.Children.Remove(selectionRectangle);
			if (overlayWindow != null)
			{
				overlayWindow.Close();
				overlayWindow = null;
			}
			if (_cancelledByUser || _selectedRegion.HasValue)
			{
				// Release only if an operation was cancelled or a region was selected, to avoid double release.
				_captureRegionSemaphore.Release();
			}
		}

		private static void OverlayWindow_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed)
			{
				startPoint = e.GetPosition(canvas);
				canvas.Children.Add(selectionRectangle);
				Canvas.SetLeft(selectionRectangle, startPoint.X);
				Canvas.SetTop(selectionRectangle, startPoint.Y);
				selectionRectangle.Width = 0;
				selectionRectangle.Height = 0;
				selectionRectangle.Visibility = Visibility.Visible;
			}
		}

		private static void OverlayWindow_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed && selectionRectangle.Visibility == Visibility.Visible)
			{
				var currentPoint = e.GetPosition(canvas);
				var x = Math.Min(currentPoint.X, startPoint.X);
				var y = Math.Min(currentPoint.Y, startPoint.Y);
				var width = Math.Max(currentPoint.X, startPoint.X) - x;
				var height = Math.Max(currentPoint.Y, startPoint.Y) - y;
				Canvas.SetLeft(selectionRectangle, x);
				Canvas.SetTop(selectionRectangle, y);
				selectionRectangle.Width = width;
				selectionRectangle.Height = height;
			}
		}

		private static void OverlayWindow_MouseUp(object sender, MouseButtonEventArgs e)
		{
			// Hide the rectangle and remove it from the canvas
			selectionRectangle.Visibility = Visibility.Hidden;
			canvas.Children.Remove(selectionRectangle);

			if (!_cancelledByUser)
			{
				// Adjust for DPI scaling
				var scalingFactor = GetScalingFactor();

				var left = (Canvas.GetLeft(selectionRectangle) + SystemParameters.VirtualScreenLeft) * scalingFactor;
				var top = (Canvas.GetTop(selectionRectangle) + SystemParameters.VirtualScreenTop) * scalingFactor;
				var width = selectionRectangle.Width * scalingFactor;
				var height = selectionRectangle.Height * scalingFactor;

				_selectedRegion = new Rectangle((int)left, (int)top, (int)width, (int)height);
				ReleaseResources();
			}
		}

		private static double GetScalingFactor()
		{
			using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
			{
				// System DPI
				float dpiX = g.DpiX;
				// Assuming 96 DPI is the system's default scaling (100%)
				return dpiX / 96;
			}
		}

		#endregion

		#region Helper Methods

		[DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool GetWindowRect(IntPtr hwnd, out Rectangle lpRect);

		/// <summary>
		/// Get temp path.
		/// </summary>
		/// <param name="folderPath"></param>
		/// <param name="format"></param>
		/// <returns></returns>
		public static string PrepareFilePath(string folderPath, System.Drawing.Imaging.ImageFormat format)
		{
			folderPath = folderPath ?? Path.GetTempPath();
			string fileName = $"Capture_{DateTime.Now:yyyyMMddHHmmss}.{format.ToString().ToLower()}";
			string fullPath = Path.Combine(folderPath, fileName);
			return fullPath;
		}

		private static void CaptureAndSave(Rectangle bounds, string filePath, ImageFormat format)
		{
			using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(bounds.Width, bounds.Height))
			{
				using (Graphics g = Graphics.FromImage(bitmap))
				{
					// Adjust for screens positioned to the left or above the primary screen
					// where bounds.Location may result in negative X or Y values.
					System.Drawing.Point sourcePoint = new System.Drawing.Point(
						Math.Max(0, bounds.X),
						Math.Max(0, bounds.Y)
					);

					System.Drawing.Rectangle captureRect = new System.Drawing.Rectangle(
						// If the bounds are negative, start capturing from 0,0 of the source screen
						// Otherwise, use the bounds as is.
						Math.Abs(bounds.X),
						Math.Abs(bounds.Y),
						bounds.Width,
						bounds.Height
					);

					g.CopyFromScreen(sourcePoint, System.Drawing.Point.Empty, captureRect.Size);
				}
				var fi = new FileInfo(filePath);
				if (!fi.Directory.Exists)
					fi.Directory.Create();
				bitmap.Save(filePath, format);
			}
		}

		#endregion


	}
}
