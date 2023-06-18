using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
namespace JocysCom.VS.AiCompanion.Engine
{
	public static class ClipboardHelper
	{
		public static void ProcessDataAndPaste()
		{
			// Get the previously selected application
			var previousApp = GetPreviousApp();

			if (previousApp != null)
			{
				try
				{
					// Copy data to clipboard
					CopyToClipboard("Your data to be copied");

					// Process the data
					var processedData = ProcessData("Data to be processed");

					// Focus on the previous application
					FocusOnApp(previousApp);

					// Wait for the application to activate
					WaitForAppActivation(previousApp);

					// Paste the processed data
					PasteFromClipboard();
				}
				catch (Exception ex)
				{
					// Handle any exceptions that may occur during the process
					MessageBox.Show($"An error occurred: {ex.Message}");
				}
			}
			else
			{
				MessageBox.Show("No previous application found.");
			}
		}

		private static Process GetPreviousApp()
		{
			// Get the currently active application process
			var currentApp = Process.GetCurrentProcess();

			// Get the previously active application
			var previousApp = GetPreviousProcess(currentApp);

			return previousApp;
		}

		private static Process GetPreviousProcess(Process currentProcess)
		{
			// Get the collection of all running processes
			var processes = Process.GetProcesses();

			Process previousProcess = null;

			// Find the process that was active before the current process
			foreach (var process in processes)
			{
				if (process.Id != currentProcess.Id && process.MainWindowHandle != IntPtr.Zero)
				{
					previousProcess = process;
					break;
				}
			}

			return previousProcess;
		}

		private static void CopyToClipboard(string data)
		{
			// Copy data to clipboard
			Clipboard.SetText(data);
		}

		private static string ProcessData(string data)
		{
			// Process the data here, e.g., perform calculations or transformations
			return data.ToUpper();
		}

		private static void FocusOnApp(Process appProcess)
		{
			// Focus on the application by bringing its main window to the foreground
			var mainWindowHandle = appProcess.MainWindowHandle;
			NativeMethods.SetForegroundWindow(mainWindowHandle);
		}

		private static void WaitForAppActivation(Process appProcess)
		{
			// Wait for the application to activate
			appProcess.WaitForInputIdle();
		}

		private static void PasteFromClipboard()
		{
			// Simulate keyboard shortcuts to paste from clipboard (e.g., Ctrl+V)
			NativeMethods.PressKey(NativeMethods.VK_CONTROL);
			NativeMethods.KeyPress(NativeMethods.VK_V);
			NativeMethods.ReleaseKey(NativeMethods.VK_CONTROL);
		}


		public static class NativeMethods
		{
			[DllImport("user32.dll")]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool SetForegroundWindow(IntPtr hWnd);

			public const byte VK_CONTROL = 0x11;
			public const byte VK_V = 0x56;

			[DllImport("user32.dll")]
			public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

			public static void PressKey(byte keyCode)
			{
				keybd_event(keyCode, 0, 0, UIntPtr.Zero);
			}

			public static void ReleaseKey(byte keyCode)
			{
				keybd_event(keyCode, 0, 0x0002, UIntPtr.Zero);
			}

			public static void KeyPress(byte keyCode)
			{
				PressKey(keyCode);
				ReleaseKey(keyCode);
			}
		}

	}
}
