using System;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Interop;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Shared
{
	/// <summary>
	/// Interaction logic for TargetOverlayWindow.xaml
	/// </summary>
	public partial class TargetOverlayWindow : Window
	{
		public TargetOverlayWindow()
		{
			InitializeComponent();
			Loaded += TargetOverlayWindow_Loaded;
		}

		private void TargetOverlayWindow_Loaded(object sender, RoutedEventArgs e)
		{
			// Get the window handle
			IntPtr hwnd = new WindowInteropHelper(this).Handle;

			// Get the current extended window style
			int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

			// Modify the extended window style to include WS_EX_TRANSPARENT and WS_EX_LAYERED
			SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
		}

		// Override to prevent AutomationPeer creation
		protected override AutomationPeer OnCreateAutomationPeer()
		{
			// Return null to prevent the AutomationPeer from being created
			return null;
		}

		// Import necessary Win32 APIs
		[System.Runtime.InteropServices.DllImport("user32.dll")]
		static extern int GetWindowLong(IntPtr hWnd, int nIndex);

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

		private const int GWL_EXSTYLE = -20;
		private const int WS_EX_TRANSPARENT = 0x00000020;
		private const int WS_EX_LAYERED = 0x00080000;
	}
}
