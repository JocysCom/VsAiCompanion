using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;

namespace JocysCom.ClassLibrary.Controls
{

	/*
	// Subscribe to the DisplaySettingsChanged event
	SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;

	private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
	{
		// Adjust window position if it's outside the virtual screen bounds
		SavePosition(this);
		LoadPosition(this);
	}
	*/

	/// <summary>
	/// Save and restore the position and state of a window. Supports multiple screens and DPI scaling.
	/// If the window parts are outside of the visible working area, moves the window into the visible working area.
	/// </summary>
	public class PositionSettings
	{
		public double Left { get; set; }
		public double Top { get; set; }
		public double Width { get; set; }
		public double Height { get; set; }
		public string ScreenName { get; set; }
		public WindowState WindowState { get; set; }
		public bool IsEnabled { get; set; }

		/// <summary>
		/// Call on Window_Closing event.
		/// </summary>
		public void SavePosition(Window w)
		{
			// Get virtual screen
			var sLeft = SystemParameters.VirtualScreenLeft;
			var sTop = SystemParameters.VirtualScreenTop;
			var sWidth = SystemParameters.VirtualScreenWidth;
			var sHeight = SystemParameters.VirtualScreenHeight;
			// Set windows state.
			WindowState = w.WindowState;
			// Set position.
			var pixRectangle = new Rect(w.Left, w.Top, w.Width, w.Height);
			Left = pixRectangle.Left;
			Top = pixRectangle.Top;
			Width = pixRectangle.Width;
			Height = pixRectangle.Height;
			// Set screen name.
			var adjustedScreenBounds = Screen.AllScreens.ToDictionary(x => x, x => NativeMethods.GetAdjustedScreenBounds(x));
			ScreenName = (adjustedScreenBounds.FirstOrDefault(x => x.Value.Screen.IntersectsWith(pixRectangle)).Key ?? Screen.PrimaryScreen).DeviceName;
			// Enable settings for loading.
			IsEnabled = true;
		}

		public event EventHandler PositionLoaded;

		public void RaisePositionLoaded()
			=> PositionLoaded?.Invoke(this, EventArgs.Empty);

		/// <summary>
		/// Call on Window_SourceInitialized event.
		/// </summary>
		public void LoadPosition(Window w, WindowState? overrideState = null)
		{
			if (!IsEnabled)
				return;

			// Clamp the width and height to the specified minimum and maximum values of the form.
			if (w.MinWidth > 0) Width = Math.Max(Width, w.MinWidth);
			if (w.MinHeight > 0) Height = Math.Max(Height, w.MinHeight);
			if (w.MaxWidth > 0) Width = Math.Min(Width, w.MaxWidth);
			if (w.MaxHeight > 0) Height = Math.Min(Height, w.MaxHeight);

			// Get window bounds.
			var pixRectangle = new Rect(Left, Top, Width, Height);

			// Get virtual screen
			var sLeft = SystemParameters.VirtualScreenLeft;
			var sTop = SystemParameters.VirtualScreenTop;
			var sWidth = SystemParameters.VirtualScreenWidth;
			var sHeight = SystemParameters.VirtualScreenHeight;

			// Move the window into the multi-monitor virtual screen area if it's outside of it.
			if (pixRectangle.Left < sLeft)
				pixRectangle.X = sLeft;
			if (pixRectangle.Top < sTop)
				pixRectangle.Y = sTop;
			if (pixRectangle.Right > sLeft + sWidth)
				pixRectangle.X = sLeft + sWidth - pixRectangle.Width;
			if (pixRectangle.Bottom > sTop + sHeight)
				pixRectangle.Y = sTop + sHeight - pixRectangle.Height;

			// Move the window into screen working area (one that exclude Windows toolbar) if it's outside of it.
			// Get screen bounds adjusted for the DPI scaling.
			var adjustedScreenBounds = Screen.AllScreens.ToDictionary(x => x, x => NativeMethods.GetAdjustedScreenBounds(x));
			var adjusted = adjustedScreenBounds.FirstOrDefault(x => x.Value.Screen.IntersectsWith(pixRectangle));
			var screen = adjusted.Value;
			var adjustedBounds = adjusted.Key;
			// If Window screen is found then...
			if (screen != null)
			{
				var sBounds = adjusted.Value.Screen;
				var wBounds = adjusted.Value.WorkingArea;
				// Make sure the width and height fits within the working area.
				Width = Math.Min(Width, wBounds.Width);
				Height = Math.Min(Height, wBounds.Height);
				// If top/bottom outside of visible working area then...
				if (!IsWindowFullyVisible(pixRectangle))
				{
					// Move the window into the working area.
					var newTop = Math.Max(wBounds.Top, Math.Min(pixRectangle.Top, wBounds.Bottom - pixRectangle.Height));
					pixRectangle = new Rect(pixRectangle.Left, newTop, pixRectangle.Width, pixRectangle.Height);
				}
				// If left/right outside of visible working area then...
				if (!IsWindowFullyVisible(pixRectangle))
				{
					// Move the window into the working area.
					var newLeft = Math.Max(wBounds.Left, Math.Min(pixRectangle.Left, wBounds.Right - pixRectangle.Width));
					pixRectangle = new Rect(newLeft, pixRectangle.Top, pixRectangle.Width, pixRectangle.Height);
				}
			}

			// Restore position and state.
			w.Left = pixRectangle.Left;
			w.Top = pixRectangle.Top;
			w.Width = pixRectangle.Width;
			w.Height = pixRectangle.Height;
			w.WindowState = overrideState ?? WindowState;

			RaisePositionLoaded();
		}

		#region Helper Functions

		public static Size ConvertToPixels(Size size, Visual v = null)
		{
			var matrix = GetMatrix(v);
			return new Size(size.Width * matrix.M11, size.Height * matrix.M22);
		}

		public static Point ConvertToPixels(Point point, Visual v = null)
		{
			var matrix = GetMatrix(v);
			return new Point(point.X * matrix.M11, point.Y * matrix.M22);
		}

		/// <summary>
		/// Converts a point from physical pixels to device-independent units (DIUs), considering the DPI scaling at the given point.
		/// </summary>
		/// <param name="pixPoint">The point in physical pixels to convert.</param>
		/// <returns>The point converted to device-independent units (DIUs).</returns>
		public static Point ConvertToDiu(Point pixPoint)
		{
			var (scaleX, scaleY) = GetScalingFactorsAtPoint(pixPoint);
			return new Point(pixPoint.X / scaleX, pixPoint.Y / scaleY);
		}

		/// <summary>
		/// Converts a size from physical pixels to device-independent units (DIUs), considering the DPI scaling at the location of a specified point.
		/// </summary>
		/// <param name="pixSize">The size in physical pixels to convert.</param>
		/// <param name="pixPoint">A point representing the location where the size applies, used to determine the DPI scaling.</param>
		/// <returns>The size converted to device-independent units (DIUs).</returns>
		public static Size ConvertToDiu(Size pixSize, Point pixPoint)
		{
			var (scaleX, scaleY) = GetScalingFactorsAtPoint(pixPoint);
			return new Size(pixSize.Width / scaleX, pixSize.Height / scaleY);
		}

		/// <summary>
		/// Convert the screen rectangle to device-independent units rectangle.
		/// </summary>
		static Rect ConvertToDiu(Rect pixRect)
		{
			var (scaleX, scaleY) = GetScalingFactorsAtPoint(pixRect.Location);
			var location = new Point(pixRect.X / scaleX, pixRect.Y / scaleY);
			var size = new Size(pixRect.Width / scaleX, pixRect.Height / scaleY);
			return new Rect(location, size);
		}

		/// <summary>
		/// Retrieves the scaling factors (DPI scaling) at the specified point by determining the monitor's DPI settings.
		/// </summary>
		/// <param name="point">The point for which to retrieve the scaling factors.</param>
		/// <returns>A tuple containing the scaling factors for the X and Y axes.</returns>
		public static (double scaleX, double scaleY) GetScalingFactorsAtPoint(Point point)
		{
			// Determine the monitor that contains the point
			var monitor = NativeMethods.MonitorFromPoint(
				new NativeMethods.POINT { x = (int)point.X, y = (int)point.Y },
				NativeMethods.MONITOR_DEFAULTTONEAREST);

			if (monitor == IntPtr.Zero)
			{
				// Fallback to system DPI scaling if monitor not found
				var matrix = GetMatrix();
				return (matrix.M11, matrix.M22);
			}

			// Get DPI for the monitor
			uint dpiX, dpiY;
			int result = NativeMethods.GetDpiForMonitor(
				monitor,
				NativeMethods.DpiType.Effective,
				out dpiX,
				out dpiY);

			if (result != 0)
			{
				// Log the error
				System.Diagnostics.Debug.WriteLine($"GetDpiForMonitor failed with result {result}");
				// Fallback to system DPI scaling if unable to get monitor DPI
				var matrix = GetMatrix();
				return (matrix.M11, matrix.M22);
			}

			// Log the DPI values
			System.Diagnostics.Debug.WriteLine($"Monitor DPI: X={dpiX}, Y={dpiY}");

			// Convert DPI to scaling factor
			double scaleX = dpiX / 96.0;
			double scaleY = dpiY / 96.0;
			return (scaleX, scaleY);
		}

		/// <summary>
		/// Convert device-independent units rectangle to the screen rectangle.
		/// </summary>
		static Rect ConvertToPixels(Rect diuRectangle, Visual v = null)
		{
			var matrix = GetMatrix(v);
			var leftPixels = diuRectangle.Left * matrix.M11;
			var topPixels = diuRectangle.Top * matrix.M22;
			var widthPixels = diuRectangle.Width * matrix.M11;
			var heightPixels = diuRectangle.Height * matrix.M22;
			return new Rect(leftPixels, topPixels, widthPixels, heightPixels);
		}

		static System.Windows.Media.Matrix GetMatrix(Visual v = null)
		{
			// Get the transformation matrix for the specified visual.
			// If no visual is provided, create a temporary visual to obtain the matrix.
			// Note: This may not correctly reflect per-monitor DPI scaling in multi-monitor setups.
			var matrix = v is null ? null : PresentationSource.FromVisual(v)?.CompositionTarget?.TransformToDevice;
			if (matrix != null)
				return matrix.Value;
			// Create a temporary Visual object
			var tempVisual = new System.Windows.Shapes.Rectangle();
			// Add the Visual to a temporary PresentationSource.
			var source = new HwndSource(new HwndSourceParameters());
			source.RootVisual = tempVisual;
			// Get the DPI scaling of the target screen
			matrix = source.CompositionTarget.TransformToDevice;
			// Dispose the temporary PresentationSource or app won't close properly.
			source.Dispose();
			return matrix.Value;
		}

		public static Rect GetPixelsBoundaryRectangle(FrameworkElement element)
		{
			var point = element.PointToScreen(new Point(0, 0));
			var rect = new Rect(point, new Size(element.ActualWidth, element.ActualHeight));
			return rect;
		}

		static Rect UnionOfAllWorkingAreaBounds()
		{
			var adjustedScreenBounds = Screen.AllScreens.Select(x => NativeMethods.GetAdjustedScreenBounds(x));
			return adjustedScreenBounds.Aggregate(Rect.Empty, (union, rect) => Rect.Union(union, rect.WorkingArea));
		}

		static bool IsWindowFullyVisible(Rect windowBounds)
		{
			var screenBoundsUnion = UnionOfAllWorkingAreaBounds();
			var intersection = Rect.Intersect(screenBoundsUnion, windowBounds);
			return intersection.Width * intersection.Height == windowBounds.Width * windowBounds.Height;
		}

		#endregion

		static internal class NativeMethods
		{


			[DllImport("user32.dll")]
			internal static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

			[DllImport("Shcore.dll")]
			internal static extern int GetDpiForMonitor(IntPtr hMonitor, DpiType dpiType, out uint dpiX, out uint dpiY);

			[DllImport("user32.dll", SetLastError = true)]
			internal static extern IntPtr MonitorFromRect(ref Int32Rect lprcMonitor, uint dwFlags);

			[DllImport("shcore.dll")]
			public static extern int GetProcessDpiAwareness(IntPtr hprocess, out ProcessDpiAwareness value);

			internal const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

			[StructLayout(LayoutKind.Sequential)]
			internal struct POINT
			{
				public int x;
				public int y;
			}

			internal enum DpiType
			{
				Effective = 0,
				Angular = 1,
				Raw = 2,
			}

			public enum ProcessDpiAwareness
			{
				Process_DPI_Unaware = 0,
				Process_System_DPI_Aware = 1,
				Process_Per_Monitor_DPI_Aware = 2
			}


			public class AdjustedBounds
			{
				public Rect Screen;
				public Rect WorkingArea;
			}

			internal static AdjustedBounds GetAdjustedScreenBounds(Screen screen)
			{
				var sBounds = screen.Bounds;
				var wBounds = screen.WorkingArea;
				var monitorRect = new Int32Rect(sBounds.Left, sBounds.Top, sBounds.Right, sBounds.Bottom);
				var monitor = MonitorFromRect(ref monitorRect, MONITOR_DEFAULTTONEAREST);
				var ab = new AdjustedBounds();
				if (monitor == IntPtr.Zero)
					return ab;
				GetDpiForMonitor(monitor, DpiType.Effective, out uint dpiX, out uint dpiY);
				var scaleFactorX = (double)dpiX / 96f;
				var scaleFactorY = (double)dpiY / 96f;
				ab.Screen = new Rect(
					sBounds.Left / scaleFactorX,
					sBounds.Top / scaleFactorY,
					sBounds.Width / scaleFactorX,
					sBounds.Height / scaleFactorY
				);
				ab.WorkingArea = new Rect(
					wBounds.Left / scaleFactorX,
					wBounds.Top / scaleFactorY,
					wBounds.Width / scaleFactorX,
					wBounds.Height / scaleFactorY
				);
				return ab;
			}

		}

		#region Grid Position

		/*

		CONTROL:

		<Grid x:Name="MainGrid" SizeChanged="Grid_SizeChanged">
			<Grid.RowDefinitions>
				<RowDefinition Height="*" />
			</Grid.RowDefinitions>
			<Grid.ColumnDefinitions>
				<ColumnDefinition Width="40*" />
				<ColumnDefinition Width="Auto" />
				<ColumnDefinition Width="60*" />
			</Grid.ColumnDefinitions>
			<StackPanel x:Name="Panel0" Grid.Column="0" />
			<GridSplitter
				Grid.Column="1"
				DragCompleted="GridSplitter_DragCompleted"
				ResizeDirection="Columns"
				Style="{StaticResource GridSplitterVertical}" />
			<StackPanel x:Name="Panel2" Grid.Column="2" />
		</Grid>

		CODE:

		private bool _gridSplitterPositionSet;

		private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
		{
			// ...
			PositionSettings.SetGridSplitterPosition(grid, position);
		}

		private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (e.WidthChanged && !_gridSplitterPositionSet)
			{
				_gridSplitterPositionSet = true;
				var position = PositionSettings.GetGridSplitterPosition(grid);
				// ...
			}
		}

		*/

		/// <summary>
		/// Get Grid Splitter positon as percentage from 0 to 1.
		/// </summary>
		public static double? GetGridSplitterPosition(Grid grid, GridSplitter splitter = null)
		{
			splitter = splitter ?? grid.Children.OfType<GridSplitter>().First();
			var isVertical = splitter.ResizeDirection == GridResizeDirection.Rows;
			var size0 = isVertical
				? grid.RowDefinitions[0].ActualHeight
				: grid.ColumnDefinitions[0].ActualWidth;
			var total = isVertical
				? grid.ActualHeight
				: grid.ActualWidth;
			if (double.IsNaN(size0) && double.IsInfinity(size0))
				return null;
			if (double.IsNaN(total) && double.IsInfinity(total) || total == 0)
				return null;
			var position = size0 / total;
			return position;
		}

		/// <summary>
		/// Set Grid Splitter positon as percentage from 0 to 1.
		/// </summary>
		public static bool SetGridSplitterPosition(Grid grid, double position, GridSplitter splitter = null, bool fixedSize = false)
		{
			// If saved position value is invalid then return.
			if (double.IsNaN(position) || double.IsInfinity(position) || position < 0 || position > 1)
				return false;
			splitter = splitter ?? grid.Children.OfType<GridSplitter>().First();
			var isVertical = splitter.ResizeDirection == GridResizeDirection.Rows;
			GridLength value0;
			GridLength value2;
			if (fixedSize)
			{
				// Get size.
				var total = isVertical
					? grid.ActualHeight
					: grid.ActualWidth;
				if (total == 0)
					return false;
				// Remove splitter from total.
				value0 = new GridLength(position * total, GridUnitType.Pixel);
				value2 = new GridLength(1, GridUnitType.Star);
			}
			else
			{
				value0 = new GridLength(position * 100.0, GridUnitType.Star);
				value2 = new GridLength(100.0 - position * 100.0, GridUnitType.Star);
			}
			if (isVertical)
			{
				grid.RowDefinitions[0].Height = value0;
				grid.RowDefinitions[2].Height = value2;
			}
			else
			{
				grid.ColumnDefinitions[0].Width = value0;
				grid.ColumnDefinitions[2].Width = value2;
			}
			return true;
		}

		#endregion

	}

}
