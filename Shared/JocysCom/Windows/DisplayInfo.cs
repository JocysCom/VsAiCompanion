using System.Collections.Generic;
using System.Drawing; // For Point and Rectangle
using System.Windows;

namespace JocysCom.ClassLibrary.Windows
{
	public class DisplayInfo
	{
		/// <summary>
		/// The device name of the monitor.
		/// </summary>
		public string DeviceName { get; set; }

		/// <summary>
		/// The bounds of the monitor in physical pixels.
		/// </summary>
		public Rect PhysicalBounds { get; set; }

		/// <summary>
		/// The bounds of the monitor in Windows Forms units (pixels).
		/// </summary>
		public Rectangle FormsBounds { get; set; }

		/// <summary>
		/// The bounds of the monitor in WPF units (device-independent units).
		/// </summary>
		public Rect WpfBounds { get; set; }

		/// <summary>
		/// The scaling factor (DPI scaling) of the monitor.
		/// </summary>
		public double ScaleFactor { get; set; }

		/// <summary>
		/// The monitors adjacent to this monitor and the edges they are adjacent on.
		/// Key - The device name of the adjacent monitor.
		/// Value - The list of edges where the monitors are adjacent (Left, Top, Right, Bottom).
		/// </summary>
		public Dictionary<string, List<string>> AdjacentMonitors { get; set; } = new Dictionary<string, List<string>>();
	}
}
