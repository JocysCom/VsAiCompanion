using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace JocysCom.VS.AiCompanion.Engine.Controls.Chat
{
	/// <summary>
	/// Interaction logic for DataListControl.xaml
	/// </summary>
	public partial class ChatListControl : UserControl
	{
		public ChatListControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			ScriptingHandler = new ScriptingHandler();
			ScriptingHandler.OnMessageAction += _ScriptingHandler_OnMessageAction;
		}

		public void SetDataItems(TemplateItem item)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (Item != null)
			{
				Item.PropertyChanged -= Item_PropertyChanged;
				if (Item.Messages != null)
					Item.Messages.ListChanged -= DataItems_ListChanged;
			}
			Item = item;
			if (Item != null)
			{
				Item.PropertyChanged += Item_PropertyChanged;
				if (Item.Messages != null)
					Item.Messages.ListChanged += DataItems_ListChanged;
			}
			IsResetMessgesPending = !ScriptHandlerInitialized;
			if (ScriptHandlerInitialized)
				ControlsHelper.AppBeginInvoke(ResetWebMessages);
		}

		private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(TemplateItem.Name))
			{
				//ResetWebMessages();
			}
		}

		private bool IsResetMessgesPending;

		private void ResetWebMessages()
		{
			InvokeScript($"DeleteMessages();");
			var path = System.IO.Path.GetDirectoryName(Global.GetPath(Item));
			SetItem(path, Item.Name);
			foreach (var message in Item.Messages)
			{
				// Set message id in case data is bad and it is missing.
				if (string.IsNullOrEmpty(message.Id))
					message.Id = Guid.NewGuid().ToString("N");
				InsertWebMessage(message, false);
			}
			SetWebSettings(Item.Settings);
		}

		private void Tasks_ListChanged(object sender, ListChangedEventArgs e)
			=> UpdateUpdateButton();

		public void SetWebSettings(ChatSettings settings)
		{
			var json = JsonSerializer.Serialize(settings);
			InvokeScript($"SetSettings({json});");
		}

		public void SetItem(string location, string name)
		{
			var json = JsonSerializer.Serialize(new
			{
				Location = location,
				Name = name,
			});
			InvokeScript($"SetItem({json});");
		}

		public ChatSettings GetWebSettings()
		{
			if (!ScriptHandlerInitialized)
				return null;
			var json = (string)InvokeScript($"GetSettings();");
			if (string.IsNullOrEmpty(json))
				return null;
			try
			{
				var settings = (ChatSettings)JsonSerializer.Deserialize(json, typeof(ChatSettings));
				return settings;
			}
			catch (Exception ex)
			{
				Global.MainControl.InfoPanel.SetBodyError(ex.Message);
			}
			return null;
		}

		public void SetZoom(int zoom)
		{
			InvokeScript($"SetZoom({zoom});");
		}

		public void RemoveMessage(MessageItem message)
		{
			Item.Messages.Remove(message);
			InvokeScript($"DeleteMessage('{message.Id}');");
		}

		void InsertWebMessage(MessageItem item, bool autoScroll)
		{
			var json = JsonSerializer.Serialize(item);
			InvokeScript($"InsertMessage({json}, {autoScroll.ToString().ToLower()});");
		}

		/// <summary>
		/// Update web message from C# message.
		/// </summary>
		public bool UpdateWebMessage(MessageItem item, bool autoScroll)
		{
			var json = JsonSerializer.Serialize(item);
			var success = (bool)InvokeScript($"UpdateMessage({json}, {autoScroll.ToString().ToLower()});");
			return success;
		}

		private void DataItems_ListChanged(object sender, ListChangedEventArgs e)
		{
			if (e.ListChangedType == ListChangedType.ItemAdded)
				InsertWebMessage(Item.Messages[e.NewIndex], true);
			if (e.ListChangedType == ListChangedType.ItemChanged)
			{
				var allowUpdate =
					e.PropertyDescriptor.Name == nameof(MessageItem.Type) ||
					e.PropertyDescriptor.Name == nameof(MessageItem.Updated);
				if (allowUpdate)
					UpdateWebMessage(Item.Messages[e.NewIndex], false);
			}
		}

		public TemplateItem Item
		{
			get => _Item;
			set => _Item = value;
		}
		public TemplateItem _Item;

		public ItemType DataType { get; set; }

		private void MainDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
			=> UpdateUpdateButton();

		void UpdateUpdateButton()
		{
		}

		public WebBrowser _WebBrowser;

		private async void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			// Allow to load once.
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
				// Remove control frm the UI presets list.
				UiPresetsManager.RemoveControls(LoadingLabel);
				await Helper.Debounce(InitWebBrowser);
			}
		}

		void InitWebBrowser()
		{
			_WebBrowser = new WebBrowser();
			_WebBrowser.Name = "WebBrowser";
			_WebBrowser.Visibility = Visibility.Collapsed;
			_WebBrowser.Navigating += WebBrowser_Navigating;
			_WebBrowser.LoadCompleted += WebBrowser_LoadCompleted;
			_WebBrowser.Navigate("about:blank");
			MainGrid.Children.Add(_WebBrowser);
		}

		public static string contentsFile = "ChatListControl.html";
		public static object contentsLock = new object();
		public static string contents;

		private void WebBrowser_Navigating(object sender, System.Windows.Navigation.NavigatingCancelEventArgs e)
		{
			if (e.Uri == null)
				return;
			var fileName = e.Uri?.AbsolutePath;
			// Check if it's trying to navigate to one of our files.
			if (fileName == "blank")
			{
				// process contents only once.
				lock (contentsLock)
				{
					if (contents is null)
					{
						contents = GetResource(contentsFile);
						LoadResource(ref contents, "IconInformation.svg");
						LoadResource(ref contents, "IconQuestion.svg");
						LoadResource(ref contents, "IconWarning.svg");
						LoadResource(ref contents, "IconError.svg");
						LoadResource(ref contents, "IconIn.svg");
						LoadResource(ref contents, "IconOut.svg");
						LoadResource(ref contents, "core-js.min.js");
						LoadResource(ref contents, "marked.min.js");
						LoadResource(ref contents, "prism.css");
						LoadResource(ref contents, "prism.js");
					}
				}
				_WebBrowser.NavigateToString(contents);
				return;
			}
			if (!string.IsNullOrEmpty(e.Uri?.OriginalString))
				ControlsHelper.OpenUrl(e.Uri.OriginalString);
			// Supress all other navigation.
			e.Cancel = true;
		}

		// Don't work correctly.
		public static string MinifyJavaScript(string input)
		{
			var singleLinePattern = @"(?<![""'])//.*";
			var multiLinePattern = @"(?<![""'])/\*(.|\n)*?\*/";
			var s = input;
			s = Regex.Replace(s, singleLinePattern, "", RegexOptions.Multiline);
			s = Regex.Replace(s, multiLinePattern, "", RegexOptions.Singleline);
			return s;
		}

		/// <summary>
		/// Replace the paths to with the actual file contents.
		/// </summary>
		void LoadResource(ref string contents, string name)
		{
			byte[] data;
			if (name == "prism.js")
			{
				// Fix compatibility for WebBrowser control (Internete Explorer).
				var s = Helper.FindResource<string>(name, GetType().Assembly)
					.Replace(@"var lang = /(?:^|\s)lang(?:uage)?-([\w-]+)(?=\s|$)/i;", @"var lang = /(?:^|\s)lang(?:uage)?-([\w-]+)(?=\s|$)/ig;")
					.Replace(@"element.className = element.className.replace(RegExp(lang, 'gi'), '');", @"element.className = element.className.replace(lang, '');");
				//s = MinifyJavaScript(s);
				data = System.Text.Encoding.UTF8.GetBytes(s);
			}
			else
			{
				data = Helper.FindResource<byte[]>(name, GetType().Assembly);
			}
			contents = contents.Replace("ChatListControl/" + name, ClassLibrary.Files.Mime.GetResourceDataUri(name, data));
		}

		private void WebBrowser_LoadCompleted(object sender, System.Windows.Navigation.NavigationEventArgs e)
		{
			_WebBrowser.ObjectForScripting = ScriptingHandler;
			// Add BeginInvoke to allow the JavaScript syntax highlighter to initialize after loading.
			ControlsHelper.AppBeginInvoke(() =>
			{
				ScriptHandlerInitialized = true;
				// If messages set but messages are not loaded yet.
				if (IsResetMessgesPending)
					ControlsHelper.AppBeginInvoke(ResetWebMessages);
				if (Global.IsVsExtension)
				{
					Global.KeyboardHook.KeyDown += KeyboardHook_KeyDown;
				}
			});
		}

		public event EventHandler WebBrowserDataLoaded;

		/// <summary>
		/// This handler will be triggered from ChatListControl.html
		/// </summary>
		private void _ScriptingHandler_OnMessageAction(object sender, string[] e)
		{
			var actionString = e[1];
			if (string.IsNullOrEmpty(actionString))
				return;
			var action = (MessageAction)Enum.Parse(typeof(MessageAction), actionString);
			if (action == MessageAction.Loaded)
			{
				LoadingLabel.Visibility = Visibility.Collapsed;
				_WebBrowser.Visibility = Visibility.Visible;
				WebBrowserDataLoaded?.Invoke(this, EventArgs.Empty);
				return;
			}
			var ids = (e[0] ?? "").Split('_');
			var messageId = ids[0];
			var message = Item.Messages.FirstOrDefault(x => x.Id == messageId);
			switch (action)
			{
				case MessageAction.Remove:
					if (message != null)
						RemoveMessage(message);
					break;
				case MessageAction.Copy:
					if (message != null)
						Clipboard.SetText(message.Body);
					break;
				case MessageAction.DataCopy:
					Clipboard.SetText(e[2]);
					break;
				case MessageAction.DataApply:
					//Global.SetSelection(e[2]);
					break;
				default:
					break;
			}
		}

		#region Script Handler

		bool ScriptHandlerInitialized;

		public ScriptingHandler ScriptingHandler;

		#endregion

		#region HTML

		public object InvokeScript(string script)
		{
			if (!ScriptHandlerInitialized)
				return default;
			try
			{
				return _WebBrowser.InvokeScript("eval", new object[] { script });
			}
			catch (Exception ex)
			{
				Global.MainControl.InfoPanel.SetBodyError(ex.Message);
			}
			return default;
		}

		string GetResource(string name)
		{
			var asm = GetType().Assembly;
			var fullName = asm.GetManifestResourceNames()
				.Where(x => x.EndsWith(name))
				.First();
			var stream = asm.GetManifestResourceStream(fullName);
			var reader = new StreamReader(stream, true);
			var contents = reader.ReadToEnd();
			return contents;
		}

		#endregion

		private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			var isCtrlDown = Keyboard.Modifiers == ModifierKeys.Control;
			if (isCtrlDown && e.Key == Key.C)
			{
				bool isFocused = (bool)_WebBrowser.InvokeScript("isElementFocused");
				if (isFocused)
				{
					InvokeScript("Copy();");
					e.Handled = true;
				}
			}
		}

		private void KeyboardHook_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
		{
			// Check if CTRL+C is pressed
			if (Keyboard.Modifiers != ModifierKeys.Control || e.KeyCode != System.Windows.Forms.Keys.C)
				return;
			IntPtr foregroundWindow = NativeMethods.GetForegroundWindow();
			if (foregroundWindow == IntPtr.Zero)
				return;
			uint processId;
			NativeMethods.GetWindowThreadProcessId(foregroundWindow, out processId);
			// Assuming that the current process ID is required
			uint currentProcessId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
			if (processId != currentProcessId)
				return;
			var win = Window.GetWindow(this);
			if (win == null)
				return;
			var thisWindow = new System.Windows.Interop.WindowInteropHelper(win).Handle;
			if (foregroundWindow != thisWindow)
				return;
			// Assuming the WebBrowser control is hosted in the WPF window, you need to get its native handle.
			var window = Window.GetWindow(this);
			var helper = new System.Windows.Interop.WindowInteropHelper(window);
			var webBrowserHandle = _WebBrowser.Handle;
			if (webBrowserHandle == IntPtr.Zero)
				return;
			var focusedControl = NativeMethods.GetFocus();
			if (!IsAncestor(webBrowserHandle, focusedControl))
				return;
			// Works with the browswer warning about access to clipboard.
			InvokeScript("Copy();");
			e.Handled = true;
		}

		// Helper method to determine if webBrowserHandle is ancestor of focusedControl
		private bool IsAncestor(IntPtr ancestorHandle, IntPtr childHandle)
		{
			if (childHandle == IntPtr.Zero)
				return false;
			IntPtr parent = childHandle;
			while (parent != IntPtr.Zero)
			{
				if (parent == ancestorHandle)
					return true;
				parent = NativeMethods.GetParent(parent);
			}
			return false;
		}

		class NativeMethods
		{
			[DllImport("user32.dll", SetLastError = true)]
			internal static extern IntPtr GetForegroundWindow();

			[DllImport("user32.dll", SetLastError = true)]
			internal static extern IntPtr GetFocus();

			[DllImport("user32.dll", SetLastError = true)]
			internal static extern IntPtr GetParent(IntPtr hWnd);

			[DllImport("user32.dll", SetLastError = true)]
			internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

		}

	}
}
