using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace JocysCom.ClassLibrary.Controls
{

	public static class StartHelper
	{

		/// <summary>Stores the unique windows message id from the RegisterWindowMessage call.</summary>
		private static int _WindowMessage;

		public static event EventHandler OnRestore;
		public static event EventHandler OnClose;

		/// <summary>Used to determine if the application is already open.</summary>
		private static System.Threading.Mutex _Mutex;

		public const int wParam_Restore = 1;
		public const int wParam_Close = 2;

		public static string uid;

		/// <summary>
		/// Set unique application Id which will identify all apps sharing broadcast messages.
		/// You can use full product name. For example "JocysCom VS AI Companion App"
		/// </summary>
		/// <param name="uniqueAppId">Unique Id</param>
		public static void Initialize(string uniqueAppId)
		{
			uid = uniqueAppId;
		}

		public static void Dispose()
		{
			if (_Mutex != null)
				_Mutex.Dispose();
		}

		public const string arg_Exit = "Exit";

		/// <summary>
		/// Process command line arguments 
		/// </summary>
		public static bool AllowToRun(bool allowOneCopy)
		{
			var args = System.Environment.GetCommandLineArgs();
			var ic = new JocysCom.ClassLibrary.Configuration.Arguments(args);
			if (ic.ContainsKey(arg_Exit))
			{
				// Tell all running apps to close.
				BroadcastMessage(wParam_Close);
				return false;
			}
			// if multiple copies allowed them return true.
			if (!allowOneCopy)
				return true;
			// Returns true if other instance exists.
			// Other isntance will be restored from tray.
			var otherInstanceIsRunning = BroadcastMessage(wParam_Restore);
			return !otherInstanceIsRunning;
		}

		/// <summary>
		/// Broadcast message to other instances of this application.
		/// </summary>
		/// <param name="wParam">Send parameter to other instances of this application.</param>
		/// <returns>True - other instances exists; False - other instances doesn't exist.</returns>
		public static bool BroadcastMessage(int wParam)
		{
			// Check for previous instance of this app.
			_Mutex = new System.Threading.Mutex(false, uid);
			// Register the windows message
			_WindowMessage = NativeMethods.RegisterWindowMessage(uid, out var error);
			var firsInstance = _Mutex.WaitOne(1, true);
			// If this is not the first instance then...
			if (!firsInstance)
			{
				// Broadcast a message with parameters to another instance.
				var recipients = (int)NativeMethods.BSM.BSM_APPLICATIONS;
				var flags = NativeMethods.BSF.BSF_IGNORECURRENTTASK | NativeMethods.BSF.BSF_POSTMESSAGE;
				var ret = NativeMethods.BroadcastSystemMessage((int)flags, ref recipients, _WindowMessage, wParam, 0, out error);
			}
			return !firsInstance;
		}

		private const int WM_WININICHANGE = 0x001A;
		private const int WM_SETTINGCHANGE = WM_WININICHANGE;

		public static IntPtr CustomWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			_WndProc(msg, wParam);
			return IntPtr.Zero;
		}

		/// <summary>
		/// This function will receive broadcasted messages.
		/// See help how to attach _WndProc below.
		/// </summary>
		public static void _WndProc(int msg, IntPtr wParam)
		{
			// If message value was found then...
			if (msg == _WindowMessage)
			{
				// Show currently running instance.
				if (wParam.ToInt32() == wParam_Restore)
					OnRestore?.Invoke(null, null);
				//  Close currently running instance.
				if (wParam.ToInt32() == wParam_Close)
					OnClose?.Invoke(null, null);
			}
		}

		/*
			Windows Forms:

			/// <summary>
			/// This overrides the windows messaging processing. Be careful with this method,
			/// because this method is responsible for all the windows messages that are coming to the form.
			/// </summary>

			protected override void DefWndProc(ref Message m)
			{
				StartHelper._WndProc(m.Msg, m.WParam);
				// Let the normal windows messaging process it.
				base.DefWndProc(ref m);
			}

			WPF:

			protected override void OnSourceInitialized(EventArgs e)
			{
				base.OnSourceInitialized(e);
				var source = (System.Windows.Interop.HwndSource)PresentationSource.FromVisual(this);
				source.AddHook(StartHelper.CustomWndProc);
			}

		 */

		#region Native Methods

		internal class NativeMethods
		{
			/// <summary>
			/// Defines a new window message that is guaranteed to be unique throughout the system.
			/// The message value can be used when sending or posting messages.
			/// </summary>
			/// <param name="pString">The message to be registered.</param>
			/// <returns>
			/// If the message is successfully registered, the return value is a message identifier in the range 0xC000 through 0xFFFF.
			/// If the function fails, the return value is zero. To get extended error information, call GetLastError.
			/// </returns>
			[DllImport("user32.dll", EntryPoint = "RegisterWindowMessageA", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
			internal static extern int RegisterWindowMessage(string pString);

			public static int RegisterWindowMessage(string pString, out Exception error)
			{
				var id = RegisterWindowMessage(pString);
				error = (id == 0) ? new Exception(new Win32Exception().ToString()) : null;
				return id;
			}
			/// <summary>
			/// Sends a message to the specified recipients. The recipients can be applications, installable drivers,
			/// network drivers, system-level device drivers, or any combination of these system components. 
			/// </summary>
			/// <param name="dwFlags">The broadcast option.</param>
			/// <param name="pdwRecipients">A pointer to a variable that contains and receives information about the recipients of the message.</param>
			/// <param name="uiMessage">The message to be sent.</param>
			/// <param name="wParam">Additional message-specific information.</param>
			/// <param name="lParam">Additional message-specific information.</param>
			/// <returns>Positive value if the function succeeds, -1 if the function is unable to broadcast the message.</returns>
			[DllImport("user32.dll", EntryPoint = "BroadcastSystemMessageA", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
			internal static extern int BroadcastSystemMessage(int dwFlags, ref int pdwRecipients, int uiMessage, int wParam, int lParam);

			public static int BroadcastSystemMessage(int dwFlags, ref int pdwRecipients, int uiMessage, int wParam, int lParam, out Exception error)
			{
				var result = BroadcastSystemMessage(dwFlags, ref pdwRecipients, uiMessage, wParam, lParam);
				error = (result < 0) ? new Exception(new Win32Exception().ToString()) : null;
				return result;
			}


			/// <summary>
			/// The broadcast option.
			/// </summary>
			[Flags]
			public enum BSF : int
			{
				/// <summary>Enables the recipient to set the foreground window while processing the message.</summary>
				BSF_ALLOWSFW = 0x00000080,
				/// <summary>Flushes the disk after each recipient processes the message.</summary>
				BSF_FLUSHDISK = 0x00000004,
				/// <summary>Continues to broadcast the message, even if the time-out period elapses or one of the recipients is not responding.</summary>
				BSF_FORCEIFHUNG = 0x00000020,
				/// <summary>Does not send the message to windows that belong to the current task. This prevents an application from receiving its own message.</summary>
				BSF_IGNORECURRENTTASK = 0x00000002,
				/// <summary>Forces a nonresponsive application to time out. If one of the recipients times out, do not continue broadcasting the message.</summary>
				BSF_NOHANG = 0x00000008,
				/// <summary>Waits for a response to the message, as long as the recipient is not being unresponsive. Does not time out.</summary>
				BSF_NOTIMEOUTIFNOTHUNG = 0x00000040,
				/// <summary>Posts the message. Do not use in combination with BSF_QUERY.</summary>
				BSF_POSTMESSAGE = 0x00000010,
				/// <summary>Sends the message to one recipient at a time, sending to a subsequent recipient only if the current recipient returns TRUE.</summary>
				BSF_QUERY = 0x00000001,
				/// <summary>Sends the message using SendNotifyMessage function. Do not use in combination with BSF_QUERY.</summary>
				BSF_SENDNOTIFYMESSAGE = 0x00000100,
			}

			/// <summary>
			/// Recipients of the message.
			/// </summary>
			internal enum BSM : int
			{
				/// <summary>Broadcast to all system components.</summary>
				BSM_ALLCOMPONENTS = 0x00000000,
				/// <summary>Broadcast to all desktops. Requires the SE_TCB_NAME privilege.</summary>
				BSM_ALLDESKTOPS = 0x00000010,
				/// <summary>Broadcast to applications.</summary>
				BSM_APPLICATIONS = 0x00000008,
			}


		}

		#endregion

	}
}
