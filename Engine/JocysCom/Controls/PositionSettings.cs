﻿using System;
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
			// Get virtual scren
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
			//var diuRectangle = ConvertToDiu(pixRectangle);
			//Left = diuRectangle.Left;
			//Top = diuRectangle.Top;
			//Width = diuRectangle.Width;
			//Height = diuRectangle.Height;
			// Set screen name.
			var adjustedScreenBounds = Screen.AllScreens.ToDictionary(x => x, x => GetAdjustedScreenBounds(x));
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
			//var diuRectangle = new Rect(Left, Top, Width, Height);
			//var pixRectangle = ConvertToPixels(diuRectangle);

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
			var adjustedScreenBounds = Screen.AllScreens.ToDictionary(x => x, x => GetAdjustedScreenBounds(x));
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

		class AdjustedBounds
		{
			public Rect Screen;
			public Rect WorkingArea;
		}

		/// <summary>
		/// Convert the screen rectangle to device-independent units rectangle.
		/// </summary>
		static Rect ConvertToDiu(Rect pixRectangle, Visual v = null)
		{
			var matrix = GetMatrix(v);
			var leftDiu = pixRectangle.Left / matrix.M11;
			var topDiu = pixRectangle.Top / matrix.M22;
			var widthDiu = pixRectangle.Width / matrix.M11;
			var heightDiu = pixRectangle.Height / matrix.M22;
			return new Rect(leftDiu, topDiu, widthDiu, heightDiu);
		}

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

		public static Point ConvertToDiu(Point point, Visual v = null)
		{
			var matrix = GetMatrix(v);
			return new Point(point.X / matrix.M11, point.Y / matrix.M22);
		}

		public static Size ConvertToDiu(Size size, Visual v = null)
		{
			var matrix = GetMatrix(v);
			return new Size(size.Width / matrix.M11, size.Height / matrix.M22);
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
			var adjustedScreenBounds = Screen.AllScreens.Select(x => GetAdjustedScreenBounds(x));
			return adjustedScreenBounds.Aggregate(Rect.Empty, (union, rect) => Rect.Union(union, rect.WorkingArea));
		}

		static bool IsWindowFullyVisible(Rect windowBounds)
		{
			var screenBoundsUnion = UnionOfAllWorkingAreaBounds();
			var intersection = Rect.Intersect(screenBoundsUnion, windowBounds);
			return intersection.Width * intersection.Height == windowBounds.Width * windowBounds.Height;
		}

		#endregion

		#region Get screen bounds adjusted for the DPI scaling.

		[DllImport("Shcore.dll")]
		static extern int GetDpiForMonitor(IntPtr hMonitor, DpiType dpiType, out uint dpiX, out uint dpiY);

		[DllImport("user32.dll", SetLastError = true)]
		static extern IntPtr MonitorFromRect(ref Int32Rect lprcMonitor, uint dwFlags);

		enum DpiType
		{
			Effective = 0,
			Angular = 1,
			Raw = 2,
		}

		static AdjustedBounds GetAdjustedScreenBounds(Screen screen)
		{
			const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
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

		#endregion

		#region Grid Postion

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
