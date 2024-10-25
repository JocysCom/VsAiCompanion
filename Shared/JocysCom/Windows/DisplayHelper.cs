using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms; // For Screen

namespace JocysCom.ClassLibrary.Windows
{
	public static class DisplayHelper
	{
		/// <summary>
		/// Collects information about all monitors, including their rectangles and where the mouse pointer can cross from one monitor to another.
		/// </summary>
		/// <returns>A list of DisplayInfo objects representing each monitor.</returns>
		public static List<DisplayInfo> GetAllMonitorsInfo()
		{
			var monitors = new List<DisplayInfo>();

			// Get all screens
			var screens = Screen.AllScreens;

			foreach (var screen in screens)
			{
				var monitorInfo = new DisplayInfo();

				// Device name
				monitorInfo.DeviceName = screen.DeviceName;

				// Bounds in physical pixels
				var bounds = screen.Bounds;
				monitorInfo.PhysicalBounds = new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);

				// Forms Bounds (same as physical pixels for Windows Forms)
				monitorInfo.FormsBounds = bounds;

				// Get scaling factor for the monitor
				double scaleFactor = GetMonitorScaleFactor(screen);
				monitorInfo.ScaleFactor = scaleFactor;

				// WPF units (Device Independent Units)
				monitorInfo.WpfBounds = new Rect(
					bounds.Left / scaleFactor,
					bounds.Top / scaleFactor,
					bounds.Width / scaleFactor,
					bounds.Height / scaleFactor);

				monitors.Add(monitorInfo);
			}

			// Determine adjacent monitors
			DetermineAdjacentMonitors(monitors);

			return monitors;
		}

		/// <summary>
		/// Gets the scaling factor (DPI scaling) for a given screen.
		/// </summary>
		/// <param name="screen">The screen to get the scaling factor for.</param>
		/// <returns>The scaling factor as a double (e.g., 1.0 for 100%, 1.25 for 125%).</returns>
		private static double GetMonitorScaleFactor(Screen screen)
		{
			try
			{
				// Get the center point of the screen
				int x = screen.Bounds.Left + screen.Bounds.Width / 2;
				int y = screen.Bounds.Top + screen.Bounds.Height / 2;

				// Get the handle to the monitor
				var monitorHandle = MonitorFromPoint(new POINT { x = x, y = y }, MonitorOptions.MONITOR_DEFAULTTONEAREST);

				uint dpiX, dpiY;
				// Get the DPI for the monitor
				int result = GetDpiForMonitor(monitorHandle, Monitor_DPI_Type.MDT_Default, out dpiX, out dpiY);
				if (result != 0)
				{
					// Failed to get DPI, assume 100%
					return 1.0;
				}
				return dpiX / 96.0; // 96 DPI is the default DPI (100%)
			}
			catch
			{
				// In case of any failure, assume scaling factor of 1.0 (100%)
				return 1.0;
			}
		}

		/// <summary>
		/// Determines which monitors are adjacent to each other.
		/// </summary>
		/// <param name="monitors">The list of DisplayInfo objects.</param>
		private static void DetermineAdjacentMonitors(List<DisplayInfo> monitors)
		{
			foreach (var monitor in monitors)
			{
				foreach (var otherMonitor in monitors)
				{
					if (monitor.DeviceName == otherMonitor.DeviceName)
						continue;

					// Get the list of adjacent edges between the two monitors
					var edges = GetAdjacentEdges(monitor.PhysicalBounds, otherMonitor.PhysicalBounds);
					if (edges.Count > 0)
					{
						if (!monitor.AdjacentMonitors.ContainsKey(otherMonitor.DeviceName))
						{
							monitor.AdjacentMonitors[otherMonitor.DeviceName] = new List<string>();
						}
						monitor.AdjacentMonitors[otherMonitor.DeviceName].AddRange(edges);
					}
				}
			}
		}

		/// <summary>
		/// Determines the edges where two monitors are adjacent.
		/// </summary>
		/// <param name="rect1">The rectangle of the first monitor.</param>
		/// <param name="rect2">The rectangle of the second monitor.</param>
		/// <returns>A list of edges where the monitors are adjacent (Left, Top, Right, Bottom).</returns>
		private static List<string> GetAdjacentEdges(Rect rect1, Rect rect2)
		{
			List<string> edges = new List<string>();

			// Check for adjacency on the left edge
			if (IsApproximatelyEqual(rect1.Right, rect2.Left) &&
				rect1.Bottom > rect2.Top && rect1.Top < rect2.Bottom)
			{
				edges.Add("Right");
			}
			// Check for adjacency on the right edge
			if (IsApproximatelyEqual(rect1.Left, rect2.Right) &&
				rect1.Bottom > rect2.Top && rect1.Top < rect2.Bottom)
			{
				edges.Add("Left");
			}
			// Check for adjacency on the top edge
			if (IsApproximatelyEqual(rect1.Bottom, rect2.Top) &&
				rect1.Right > rect2.Left && rect1.Left < rect2.Right)
			{
				edges.Add("Bottom");
			}
			// Check for adjacency on the bottom edge
			if (IsApproximatelyEqual(rect1.Top, rect2.Bottom) &&
				rect1.Right > rect2.Left && rect1.Left < rect2.Right)
			{
				edges.Add("Top");
			}
			return edges;
		}

		/// <summary>
		/// Determines if two double values are approximately equal, within a small tolerance.
		/// </summary>
		/// <param name="a">First value.</param>
		/// <param name="b">Second value.</param>
		/// <param name="tolerance">Tolerance value.</param>
		/// <returns>True if values are approximately equal.</returns>
		private static bool IsApproximatelyEqual(double a, double b, double tolerance = 1.0)
		{
			return Math.Abs(a - b) <= tolerance;
		}

		#region Win32 API Functions

		private enum MonitorOptions : uint
		{
			MONITOR_DEFAULTTONULL = 0x00000000,
			MONITOR_DEFAULTTOPRIMARY = 0x00000001,
			MONITOR_DEFAULTTONEAREST = 0x00000002
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct POINT
		{
			public int x;
			public int y;
		}

		[DllImport("User32.dll")]
		private static extern IntPtr MonitorFromPoint(POINT pt, MonitorOptions dwFlags);

		private enum Monitor_DPI_Type : int
		{
			MDT_Effective_DPI = 0,
			MDT_Angular_DPI = 1,
			MDT_Raw_DPI = 2,
			MDT_Default = MDT_Effective_DPI
		}

		[DllImport("Shcore.dll")]
		private static extern int GetDpiForMonitor([In] IntPtr hMonitor, [In] Monitor_DPI_Type dpiType, [Out] out uint dpiX, [Out] out uint dpiY);

		#endregion
	}
}
