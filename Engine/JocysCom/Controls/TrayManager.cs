using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace JocysCom.ClassLibrary.Controls
{
	public partial class TrayManager : IDisposable
	{
		/// <summary>
		/// Will initialize new default window, which will own other window which can be disposed.
		/// </summary>
		public TrayManager(string mutexPrefix = null)
		{
			// Create the main application window which will take minimum amount of memory.
			// Main application window is impossible to dispose until the application closes.
			// Important: .Owner property must be set to Application.Current.MainWindow for sub-window to dispose.
			var appWindow = new Window();
			appWindow.Title = "TrayManagerAppWindow";
			// Make sure it contains handle.
			var awHelper = new WindowInteropHelper(appWindow);
			awHelper.EnsureHandle();
			Application.Current.MainWindow = appWindow;
			// Now you can start the main window.
			if (!string.IsNullOrEmpty(mutexPrefix))
				InitInstanceManager(mutexPrefix);
		}

		public void CreateTrayIcon()
		{
			// Item: Open Application.
			OpenApplicationMenu = new System.Windows.Forms.ToolStripMenuItem();
			OpenApplicationMenu.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
			OpenApplicationMenu.Click += OpenApplicationToolStripMenuItem_Click;
			// Item: Exit Menu.
			ExitMenu = new System.Windows.Forms.ToolStripMenuItem();
			ExitMenu.Text = "Exit";
			ExitMenu.Click += (sender, e) => OnExitClick?.Invoke(sender, e);
			// Tray menu.
			TrayMenuStrip = new System.Windows.Forms.ContextMenuStrip();
			TrayMenuStrip.Text = "Left click - program, Right click - menu.";
			TrayNotifyIcon = new System.Windows.Forms.NotifyIcon();
			TrayNotifyIcon.ContextMenuStrip = TrayMenuStrip;
			TrayMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
				OpenApplicationMenu,
				ExitMenu,
			});
			TrayNotifyIcon.Visible = false;
			TrayNotifyIcon.Click += TrayNotifyIcon_Click;
			TrayNotifyIcon.DoubleClick += TrayNotifyIcon_DoubleClick;
			TrayNotifyIcon.MouseClick += TrayNotifyIcon_MouseClick;
		}

		public void SetTrayFromWindow(Window window)
		{
			var icon = GetIcon(window.GetType().Assembly);
			var text = "";
			// If second+ instance then...
			if (InstanceNumber >= 2)
			{
				icon = OverlayNumberOnIcon(icon, InstanceNumber);
				text += $" ({InstanceNumber})";
			}
			OpenApplicationMenu.Image = icon.ToBitmap();
			OpenApplicationMenu.Text = "Open Application" + text;
			var notifyText = $"{window.Title}" + text;
			if (notifyText.Length >= 64)
				notifyText = notifyText.Substring(0, 64 - 1);
			TrayNotifyIcon.Text = notifyText;
			TrayNotifyIcon.Icon = icon;
			TrayNotifyIcon.Visible = true;
		}

		public void ProcessGetCommandLineArgs()
		{
			var args = System.Environment.GetCommandLineArgs();
			var ic = new JocysCom.ClassLibrary.Configuration.Arguments(args);
			// If windows state parameter was passed then...
			if (ic.ContainsKey(arg_WindowState))
			{
				switch (ic[arg_WindowState])
				{
					case nameof(WindowState.Maximized):
						RestoreFromTray(false, true);
						break;
					case nameof(WindowState.Minimized):
						MinimizeToTray(false, Settings.MinimizeToTray);
						break;
					default:
						RestoreFromTray(false, false);
						break;
				}
			}
			else
			{
				RestoreFromTray(false, false);
			}
		}

		public System.Drawing.Icon GetIcon(Assembly assembly, string resourceName = "App.ico")
		{
			var iconBytes = JocysCom.ClassLibrary.Helper.FindResource<byte[]>(resourceName, assembly);
			var ms = new MemoryStream(iconBytes);
			return new System.Drawing.Icon(ms);
		}

		public event EventHandler OnExitClick;
		public event EventHandler OnWindowSizeChanged;

		/// <summary>
		/// The secondary window must have the main window as owner in order to be disposed out correctly.
		/// </summary>
		public Window _Window;
		private WeakReference _WindowReference;
		public Window ApplicationCurrentMainWindow;

		private System.Windows.Forms.NotifyIcon TrayNotifyIcon;
		public System.Windows.Forms.ContextMenuStrip TrayMenuStrip;
		private System.Windows.Forms.ToolStripMenuItem OpenApplicationMenu;
		private System.Windows.Forms.ToolStripMenuItem ExitMenu;

		void CollectGarbage()
		{
			for (int i = 0; i < 4; i++)
			{
				GC.Collect(GC.MaxGeneration);
				GC.WaitForPendingFinalizers();
			}
		}

		#region Settings

		ITrayManagerSettings Settings { get; set; }

		public void SetSettigns(ITrayManagerSettings settings)
		{
			if (Settings != null)
				settings.PropertyChanged -= Settings_PropertyChanged;
			Settings = settings;
			if (Settings != null)
				settings.PropertyChanged += Settings_PropertyChanged;
		}

		private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			// Update controls by specific property.
			switch (e.PropertyName)
			{
				case nameof(Settings.StartWithWindows):
				case nameof(Settings.StartWithWindowsState):
					UpdateWindowsStart(Settings.StartWithWindows, Settings.StartWithWindowsState);
					break;
				default:
					break;
			}
		}

		#endregion

		#region Window Events

		public void SetWindow(Window window)
		{
			// If old window exists then...
			if (_Window != null)
			{
				_Window.StateChanged -= Window_StateChanged;
				_Window.Closing -= _Window_Closing;
				CollectGarbage();
			}
			_Window = window;
			if (window == null)
				return;
			_Window.Owner = Application.Current.MainWindow;
			_Window.StateChanged += Window_StateChanged;
			_Window.Closing += _Window_Closing;
			// Run event once to apply settings.
			Window_StateChanged(this, null);
		}


		WindowState? oldWindowState;
		object windowStateLock = new object();


		private void _Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (Settings.MinimizeOnClose)
			{
				var window = (Window)sender;
				// Cancel the close operation.
				e.Cancel = true;
				// Minimize the window.
				window.WindowState = WindowState.Minimized;
			}
			else
			{
				// Must shutdown application, because only main window will close and
				// Parent window will keep Application running.
				if (TrayNotifyIcon != null)
					TrayNotifyIcon.Visible = false;
				Application.Current.Shutdown();
			}
		}

		private void Window_StateChanged(object sender, EventArgs e)
		{
			// Track window state changes.
			lock (windowStateLock)
			{
				var newWindowState = _Window.WindowState;
				if (!oldWindowState.HasValue || oldWindowState.Value != newWindowState)
				{
					oldWindowState = newWindowState;
					// If window was minimized.
					if (newWindowState == WindowState.Minimized)
						MinimizeToTray(false, Settings.MinimizeToTray);
				}
				OnWindowSizeChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		#endregion

		#region Tray Icon

		private void TrayNotifyIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			bool isPrimaryClick = System.Windows.Forms.SystemInformation.MouseButtonsSwapped
				? e.Button == System.Windows.Forms.MouseButtons.Right
				: e.Button == System.Windows.Forms.MouseButtons.Left;
			if (isPrimaryClick)
			{
				if (_Window.WindowState == WindowState.Minimized || !IsVisible(_Window))
					RestoreFromTray();
				else
					MinimizeToTray(false, Settings.MinimizeToTray);
			}
			else
			{
				OpenTrayMenu();
			}
		}
		void OpenTrayMenu()
		{
			var mi = TrayNotifyIcon.GetType().GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
			mi.Invoke(TrayNotifyIcon, null);
		}

		private void TrayNotifyIcon_Click(object sender, EventArgs e)
		{
			//RestoreFromTray();
		}

		private void TrayNotifyIcon_DoubleClick(object sender, EventArgs e)
		{
			RestoreFromTray();
		}

		private void OpenApplicationToolStripMenuItem_Click(object sender, EventArgs e)
		{
			RestoreFromTray();
		}

		#endregion

		#region IsVisible

		// Import from user32.dll
		[DllImport("user32.dll")]
		static extern IntPtr WindowFromPoint(POINT Point);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

		[StructLayout(LayoutKind.Sequential)]
		public struct POINT
		{
			public int X;
			public int Y;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct RECT
		{
			public int Left;
			public int Top;
			public int Right;
			public int Bottom;
		}

		private bool IsVisible(Window window)
		{
			// If the window is null or not loaded then...
			if (window == null || !window.IsLoaded)
				return false;
			// If the window's content is not available then...
			if (window.Content == null)
				return false;
			IntPtr myHandle = new WindowInteropHelper(window).Handle;
			// Get the window's content bounds
			Rect contentBounds = VisualTreeHelper.GetDescendantBounds((Visual)window.Content);
			System.Windows.Point topLeft;
			System.Windows.Point bottomRight;
			try
			{
				topLeft = window.PointToScreen(contentBounds.TopLeft);
				bottomRight = window.PointToScreen(contentBounds.BottomRight);
			}
			catch
			{
				return false;
			}
			// Convert to screen coordinates
			RECT myRect = new RECT()
			{
				Left = (int)topLeft.X,
				Top = (int)topLeft.Y,
				Right = (int)bottomRight.X,
				Bottom = (int)bottomRight.Y,
			};
			// Define the points to check
			POINT[] pointsToCheck =
			{
				new POINT { X = myRect.Left + (myRect.Right - myRect.Left) / 2, Y = myRect.Top }, // Top center
				new POINT { X = myRect.Left, Y = myRect.Top }, // Top left
				new POINT { X = myRect.Right, Y = myRect.Top }, // Top right
				new POINT { X = myRect.Left, Y = myRect.Top + (myRect.Bottom - myRect.Top) / 2 }, // Middle left
				new POINT { X = myRect.Right, Y = myRect.Top + (myRect.Bottom - myRect.Top) / 2 }, // Middle right
				new POINT { X = myRect.Left + (myRect.Right - myRect.Left) / 2, Y = myRect.Bottom }, // Bottom center
				new POINT { X = myRect.Left, Y = myRect.Bottom }, // Bottom left
				new POINT { X = myRect.Right, Y = myRect.Bottom } // Bottom right
			};
			// Perform a hit-test at each point
			for (int i = 0; i < pointsToCheck.Length; i++)
			{
				var pt = pointsToCheck[i];
				var hWnd = WindowFromPoint(pt);
				// The point is covered by another window
				if (hWnd != myHandle)
					return false;
			}
			// All points are not covered by other windows
			return true;
		}

		#endregion

		#region Instance Manager

		private const int MaxInstances = 100; // Adjust as needed
		private MemoryMappedFile mmf;
		private Mutex mutex;
		public int InstanceNumber { get; private set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="mutexPrefix">"Local\\MyAppInstanceName"</param>
		/// <exception cref="InvalidOperationException"></exception>
		void InitInstanceManager(string mutexPrefix)
		{
			// Initialize or create the memory-mapped file
			mmf = MemoryMappedFile.CreateOrOpen(mutexPrefix + "Numbers", MaxInstances, MemoryMappedFileAccess.ReadWrite);
			// Mutex to synchronize access
			mutex = new Mutex(false, mutexPrefix + "Mutex");
			// Assign the smallest available number
			mutex.WaitOne();
			try
			{
				using (var accessor = mmf.CreateViewAccessor(0, MaxInstances, MemoryMappedFileAccess.ReadWrite))
				{
					for (int i = 0; i < MaxInstances; i++)
					{
						bool isTaken = accessor.ReadBoolean(i);
						if (!isTaken)
						{
							InstanceNumber = i + 1; // Numbers start at 1
							accessor.Write(i, true);
							return;
						}
					}
					throw new InvalidOperationException("Maximum number of instances reached.");
				}
			}
			finally
			{
				mutex.ReleaseMutex();
			}
		}

		void DisposeInitInstanceManager()
		{
			if (mmf == null)
				return;
			// Release the assigned number
			mutex.WaitOne();
			try
			{
				using (var accessor = mmf.CreateViewAccessor(0, MaxInstances, MemoryMappedFileAccess.Write))
					accessor.Write(InstanceNumber - 1, false);
			}
			finally
			{
				mutex.ReleaseMutex();
				mmf.Dispose();
				mutex.Dispose();
			}
		}

		public static Icon OverlayNumberOnIcon(Icon baseIcon, int number)
		{
			using (Bitmap bitmap = baseIcon.ToBitmap())
			using (Graphics g = Graphics.FromImage(bitmap))
			{
				// Define the font and brush for the number
				using (Font font = new Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0))
				using (System.Drawing.Brush brush = new SolidBrush(System.Drawing.Color.Black))
				using (SolidBrush backgroundBrush = new SolidBrush(System.Drawing.Color.Transparent))
				{
					// Measure the size of the number
					string numberStr = number.ToString();
					SizeF textSize = g.MeasureString(numberStr, font);

					// Define the position for the number (center)
					float x = (bitmap.Width - textSize.Width) / 2;
					float y = (bitmap.Height - textSize.Height) / 2;

					// Optional: Add a background circle for better visibility
					float padding = -6;
					float diameter = Math.Max(textSize.Width, textSize.Height) + padding;
					float circleX = (bitmap.Width - diameter) / 2;
					float circleY = (bitmap.Height - diameter) / 2;
					g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
					var circleBrush = new SolidBrush(System.Drawing.Color.FromArgb(255, System.Drawing.Color.LightGray));
					g.FillRectangle(circleBrush, circleX, circleY, diameter, diameter);

					// Draw the number
					g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
					g.DrawString(numberStr, font, brush, x, y);
				}

				// Convert the modified bitmap back to an icon
				IntPtr hIcon = bitmap.GetHicon();
				Icon resultIcon = Icon.FromHandle(hIcon);
				return resultIcon;
			}
		}

		#endregion

		#region ■ Minimize / Restore

		/// <summary>
		/// Minimize the window and hide it from the TaskBar. 
		/// </summary>
		public void MinimizeToTray(bool showBalloonTip, bool minimizeToTray)
		{
			var asm = new JocysCom.ClassLibrary.Configuration.AssemblyInfo();
			// Show only first time.
			if (showBalloonTip)
			{
				TrayNotifyIcon.BalloonTipText = asm.Product;
				// Show balloon tip for 2 seconds.
				TrayNotifyIcon.ShowBalloonTip(2, asm.Title, asm.Product, System.Windows.Forms.ToolTipIcon.Info);
			}
			if (_Window != null)
			{
				if (_Window.WindowState != WindowState.Minimized)
					_Window.WindowState = WindowState.Minimized;
				// Hide form bar from the TaskBar.
				if (minimizeToTray && _Window.ShowInTaskbar)
					_Window.ShowInTaskbar = false;
			}
		}

		public void RestoreFromTray(bool activate = false, bool maximize = false)
		{
			_WindowReference = new WeakReference(null);
			Task.Run(() => _RestoreFromTray(activate, maximize));
		}

		/// <summary>
		/// Restores the window.
		/// </summary>
		public void _RestoreFromTray(bool activate = false, bool maximize = false)
		{
			// Need isolator or app freeze.
			Action isolator = () =>
			{
				//// Set owner to properly dispose after closing.
				//mw.Owner = Application.Current.MainWindow;
				//// Initialize main window.
				//var loadedSemaphore = new SemaphoreSlim(0);
				//var closedSemaphore = new SemaphoreSlim(0);
				//mw.Loaded += (sender, e) => loadedSemaphore.Release();
				//mw.Closed += (sender, e) => SetWindow(null);
				//// Unloaded will be executed after 'Closed' event.
				//mw.Unloaded += (sender, e) =>
				//{
				//	// Global._MainWindow will be used by other controls to detach events,
				//	// therefore destroy reference by setting to null inside unloaded event.
				//	Global._MainWindow = null;
				//};
				//SetWindow(mw);
				//// Show window.
				//mw.Show();
				//loadedSemaphore.Wait();
				if (activate)
				{
					// Note: FormWindowState.Minimized and FormWindowState.Normal was used to make sure that Activate() wont fail because of this:
					// Windows NT 5.0 and later: An application cannot force a window to the foreground while the user is working with another window.
					// Instead, SetForegroundWindow will activate the window (see SetActiveWindow) and call theFlashWindowEx function to notify the user.
					if (_Window.WindowState != WindowState.Minimized)
						_Window.WindowState = WindowState.Minimized;
					_Window.Activate();
				}
				// Show in task bar before restoring windows state in order to prevent flickering.
				if (!_Window.ShowInTaskbar)
					_Window.ShowInTaskbar = true;
				// Update window state.
				var tagetState = maximize ? WindowState.Maximized : WindowState.Normal;
				if (_Window.WindowState != tagetState)
					_Window.WindowState = tagetState;
				_Window.Show();
				// Bring form to the front.
				var tm = _Window.Topmost;
				_Window.Topmost = true;
				_Window.Topmost = tm;
				_Window.BringIntoView();
			};
			ControlsHelper.AppBeginInvoke(isolator);
		}

		private static void _CollectGarbage()
		{
			// Try to remove object from the memory.
			GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
			//GC.Collect();
			GC.WaitForFullGCComplete();
			GC.WaitForPendingFinalizers();
		}

		public static void CollectGarbage(Func<bool> whileCondition = null)
		{
			if (whileCondition == null)
			{
				_CollectGarbage();
				return;
			}
			var stopwatch = new Stopwatch();
			stopwatch.Start();
			// loop untill object allive, but no longer than  seconds.
			while (whileCondition() && stopwatch.ElapsedMilliseconds < 4000)
			{
				Task.Delay(200).Wait();
				_CollectGarbage();
			}
		}

		#endregion

		#region ■ Operation 

		public void UpdateWindowsStart(bool enabled, WindowState startState)
		{
			if (enabled)
			{
				// Pick one only.
				UpdateWindowsStartRegistry(enabled, startState);
				//UpdateWindowsStartFolder(enabled, startState);
			}
			else
			{
				UpdateWindowsStartRegistry(enabled, startState);
				UpdateWindowsStartFolder(enabled, startState);
			}
		}

		public const string arg_WindowState = nameof(WindowState);

		/// <summary>
		/// Enable or disable application start with Windows after sign-in.
		/// Requires no special permissions, because current used have full access to CurrentUser 'Run' registry key.
		/// </summary>
		/// <param name="enabled">Start with Windows after Sign-In.</param>
		/// <param name="startState">Start Mode.</param>
		public void UpdateWindowsStartRegistry(bool enabled, WindowState startState)
		{
			var ai = new JocysCom.ClassLibrary.Configuration.AssemblyInfo();
			var runKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
			if (enabled)
			{
				// Fix possible issues, dot notations and invalid path separator.
				var fullPath = Path.GetFullPath(ai.AssemblyPath);
				// Add the value in the registry so that the application runs at start-up
				var command = string.Format("\"{0}\" /{1}={2}", fullPath, arg_WindowState, startState.ToString());
				var value = (string)runKey.GetValue(ai.Product);
				if (value != command)
					runKey.SetValue(ai.Product, command);
			}
			else
			{
				// Remove the value from the registry so that the application doesn't start automatically.
				if (runKey.GetValueNames().Contains(ai.Product))
					runKey.DeleteValue(ai.Product, false);
			}
			runKey.Close();
		}

		/// <summary>
		/// Enable or disable application start with Windows after sign-in
		/// Requires no special permissions, because current used have full access to CurrentUser 'Startup' folder.
		/// </summary>
		/// <param name="enabled">Start with Windows after sign-in.</param>
		/// <param name="startState">Start Mode.</param>
		public void UpdateWindowsStartFolder(bool enabled, WindowState startState)
		{
			var ai = new JocysCom.ClassLibrary.Configuration.AssemblyInfo();
			var startupFolder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Startup);
			var shortcutPath = $"{startupFolder}\\{ai.Product}.lnk";
			if (enabled)
			{
				// Fix possible issues, dot notations and invalid path separator.
				var targetPath = Path.GetFullPath(ai.AssemblyPath);
				// Add the value in the registry so that the application runs at start-up
				//var arguments = $"/{Program.arg_WindowState}={startState}";
				var windowsStyle = 1; // Normal
				if (startState == WindowState.Maximized)
					windowsStyle = 3;
				if (startState == WindowState.Minimized)
					windowsStyle = 7;
				string powershellCommand = "-NoProfile -Command " +
					$"$wShell = New-Object -ComObject WScript.Shell; " +
					$"$shortcut = $wShell.CreateShortcut('{shortcutPath}'); " +
					$"$shortcut.TargetPath = '\"{targetPath}\"'; " +
					$"$shortcut.WindowStyle = '{windowsStyle}'; " +
					//$"$shortcut.Arguments = '{arguments}'; " +
					$"$shortcut.Save();";
				using (var process = new Process())
				{
					process.StartInfo.UseShellExecute = true;
					process.StartInfo.FileName = "powershell";
					process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
					process.StartInfo.Arguments = powershellCommand;
					process.Start();
				}
			}
			else
			{
				// Remove shortcut so that the application doesn't start automatically.
				if (File.Exists(shortcutPath))
					File.Delete(shortcutPath);
			}
		}

		#endregion

		#region ■ IDisposable

		public bool IsDisposing;
		public void Dispose()
		{
			Dispose(true);
			DisposeInitInstanceManager();
			GC.SuppressFinalize(this);
		}

		// The bulk of the clean-up code is implemented in Dispose(bool)
		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				IsDisposing = true;
				if (TrayNotifyIcon != null)
					TrayNotifyIcon.Visible = false;
				TrayNotifyIcon?.Dispose();
				TrayMenuStrip?.Dispose();
				OpenApplicationMenu?.Dispose();
				ExitMenu?.Dispose();
			}
		}

		#endregion


	}
}
