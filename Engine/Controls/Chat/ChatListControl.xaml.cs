using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Files;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
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
			WebBrowserHostObject = new BrowserHostObject();
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
				_ = Helper.Debounce(ResetWebMessages);
		}

		private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(TemplateItem.Name))
			{
				// Usually when title is regenerated.
				_ = Helper.Debounce(ResetWebMessages);
			}
		}

		private bool IsResetMessgesPending;

		private void ResetWebMessages()
		{
			ControlsHelper.AppBeginInvoke(() => _ = ResetWebMessagesDebounced());
		}

		private async Task ResetWebMessagesDebounced()
		{
			await InvokeScriptAsync($"DeleteMessages();");
			var path = System.IO.Path.GetDirectoryName(Global.GetPath(Item));
			await SetItemAsync(path, Item.Name);
			foreach (var message in Item.Messages)
			{
				// Set message id in case data is bad and it is missing.
				if (string.IsNullOrEmpty(message.Id))
					message.Id = Guid.NewGuid().ToString("N");
				await InsertWebMessage(message, false);
			}
			await SetWebSettingsAsync(Item.Settings);
		}

		public async Task SetWebSettingsAsync(ChatSettings settings)
		{
			var json = JsonSerializer.Serialize(settings);
			await InvokeScriptAsync($"SetSettings({json});");
		}

		public async Task SetItemAsync(string location, string name)
		{
			var json = JsonSerializer.Serialize(new
			{
				Location = ".",
				Name = name,
			});
			await InvokeScriptAsync($"SetItem({json});");
		}

		public async Task<ChatSettings> GetWebSettingsAsync()
		{
			if (!ScriptHandlerInitialized)
				return null;
			var json = (string)await InvokeScriptAsync($"GetSettings();");
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
			_ = InvokeScriptAsync($"SetZoom({zoom});");
		}

		public async Task RemoveMessageAsync(MessageItem message)
		{
			Item.Messages.Remove(message);
			await InvokeScriptAsync($"DeleteMessage('{message.Id}');");
		}

		async Task InsertWebMessage(MessageItem item, bool autoScroll)
		{
			var json = JsonSerializer.Serialize(item);
			await InvokeScriptAsync($"InsertMessage({json}, {autoScroll.ToString().ToLower()});");
		}

		/// <summary>
		/// Update web message from C# message.
		/// </summary>
		public async Task UpdateWebMessage(MessageItem item, bool autoScroll)
		{
			var json = JsonSerializer.Serialize(item);
			await InvokeScriptAsync($"UpdateMessage({json}, {autoScroll.ToString().ToLower()});");
		}

		private async void DataItems_ListChanged(object sender, ListChangedEventArgs e)
		{
			if (e.ListChangedType == ListChangedType.ItemAdded)
				await InsertWebMessage(Item.Messages[e.NewIndex], true);
			if (e.ListChangedType == ListChangedType.ItemChanged)
			{
				var allowUpdate =
					e.PropertyDescriptor.Name == nameof(MessageItem.Type) ||
					e.PropertyDescriptor.Name == nameof(MessageItem.Updated);
				if (allowUpdate)
					await UpdateWebMessage(Item.Messages[e.NewIndex], false);
			}
		}

		public TemplateItem Item
		{
			get => _Item;
			set => _Item = value;
		}
		public TemplateItem _Item;

		public ItemType DataType { get; set; }

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
				await InitWebView2();
			}
		}

		public WebView2 _WebView2;

		async Task InitWebView2()
		{
			_WebView2 = new WebView2();
			_WebView2.Name = "WebView2";
			_WebView2.Visibility = Visibility.Collapsed;
			_WebView2.NavigationStarting += WebView2_NavigationStarting;
			MainGrid.Children.Add(_WebView2);
			if (_WebView2.CoreWebView2 == null)
			{
				_WebView2.CoreWebView2InitializationCompleted += _WebView2_CoreWebView2InitializationCompleted;
				// Workaround: "Access is denied. (Exception from HRESULT: 0x80070005(E_ACCESSDENIED))"
				// Specify a user data folder that extension can write to.
				var tempFolderPath = AppHelper.GetTempFolderPath();
				// Separate folders for extension and app to avoid locking issues.
				var suffix = Global.IsVsExtension ? "VS" : "App";
				var userDataFolder = Path.Combine(tempFolderPath, $"WebView2.{suffix}");
				// Ensure the directory exists
				if (!Directory.Exists(userDataFolder))
					Directory.CreateDirectory(userDataFolder);
				// Create CoreWebView2Environment with the specified user data folder
				var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
				// Initialize WebView2 with the environment
				await _WebView2.EnsureCoreWebView2Async(env);
			}
		}

		private void CoreWebView2_WebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
		{
			var uri = new Uri(e.Request.Uri);
			if (uri.Host == appAssetsHost)
			{
				var name = Uri.UnescapeDataString(uri.Segments.Last());
				MemoryStream stream = null;
				// If task or template item content then...
				if (uri.Segments.Length > 2 && Uri.UnescapeDataString(uri.Segments[1]) == Item.Name + "/")
				{
					var folderPath = Global.GetPath(Item);
					var fileFullPath = Path.Combine(folderPath, name);
					if (File.Exists(fileFullPath))
					{
						var bytes = System.IO.File.ReadAllBytes(fileFullPath);
						stream = new MemoryStream(bytes);
					}
				}
				else
				{
					if (name == "favicon.ico")
						name = "App.ico";
					var bytes = GetChatResource(name);
					if (bytes == null || bytes.Length == 0)
					{
						Global.MainControl.ErrorsPanel.ErrorsLogPanel.Add($"Error: Can't find '{name}' resource!");
						return;
					}
					stream = new MemoryStream(bytes);
				}
				if (stream != null)
				{
					var ext = System.IO.Path.GetExtension(name);
					var contentType = Mime.GetMimeContentType(ext);
					var response = _WebView2.CoreWebView2.Environment.CreateWebResourceResponse(
						stream, 200, "OK", $"Content-Type: {contentType}");
					e.Response = response;
				}
			}
		}

		private void WebView2_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
		{
			if (e.Uri == null)
				return;
			var uri = e.Uri;
			// Check if it's trying to navigate to one of our files.
			if (uri.StartsWith($"http://{appAssetsHost}/"))
				return;
			if (!string.IsNullOrEmpty(e.Uri))
				ControlsHelper.OpenUrl(e.Uri);
			// Suppress all other navigation.
			e.Cancel = true;
		}

		public BrowserHostObject WebBrowserHostObject;

		// According to RFC 6761, the `.invalid` domain is intended for
		// use in online construction of domain names that are sure to be invalid
		// and which should not be looked up in the DNS via the normal resolution mechanism.
		private string appAssetsHost = "appassets.invalid";

		private async void _WebView2_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
		{
			if (!e.IsSuccess)
			{
				Global.MainControl.InfoPanel.SetBodyError("WebView2 initialization failed: " + e.InitializationException.Message);
				Global.MainControl.ErrorsPanel.ErrorsLogPanel.Add(e.InitializationException.ToString());
				return;
			}
			try
			{
				await Task.Delay(0);

				WebBrowserHostObject.OnMessageAction += WebBrowserHostObject_OnMessageAction;
				_WebView2.CoreWebView2.AddHostObjectToScript("external", WebBrowserHostObject);

				await _WebView2.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
					"console.log(\"ExecuteOnDocumentCreated\");"
				);
				// Add a filter to override resources.
				_WebView2.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
				_WebView2.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;
				var tempFolderPath = AppHelper.GetTempFolderPath();
				// Set initial source after initialization
				_WebView2.CoreWebView2.Navigate($"http://{appAssetsHost}/ChatListControl.html");
			}
			catch (Exception ex)
			{
				Global.MainControl.InfoPanel.SetBodyError("WebView2 initialization failed: " + ex.Message);
				Global.MainControl.ErrorsPanel.ErrorsLogPanel.Add(ex.ToString());
			}
		}

		private void WebBrowserHostObject_OnMessageAction(object sender, (string id, string action, string data) e)
		{
			if (string.IsNullOrEmpty(e.action))
				return;
			var action = (MessageAction)Enum.Parse(typeof(MessageAction), e.action);
			if (action == MessageAction.Loaded)
			{
				LoadingLabel.Visibility = Visibility.Collapsed;
				_WebView2.Visibility = Visibility.Visible;
				WebBrowserDataLoaded?.Invoke(this, EventArgs.Empty);
				ScriptHandlerInitialized = true;
				// If messages set but messages are not loaded yet.
				if (IsResetMessgesPending)
					_ = Helper.Debounce(ResetWebMessages);
				if (Global.IsVsExtension)
					Global.KeyboardHook.KeyDown += KeyboardHook_KeyDown;
				return;
			}
			var ids = (e.id ?? "").Split('_');
			var messageId = ids[0];
			var message = Item.Messages.FirstOrDefault(x => x.Id == messageId);
			switch (action)
			{
				case MessageAction.Remove:
					if (message != null)
						_ = RemoveMessageAsync(message);
					break;
				case MessageAction.Copy:
					if (message != null)
						Clipboard.SetText(message.Body);
					break;
				case MessageAction.DataCopy:
					Clipboard.SetText(e.data);
					break;
				case MessageAction.DataApply:
					//Global.SetSelection(e[2]);
					break;
				default:
					break;
			}
		}

		[ClassInterface(ClassInterfaceType.AutoDual)]
		[ComVisible(true)]
		public class BrowserHostObject
		{
			public event EventHandler<(string id, string action, string data)> OnMessageAction;
			public void ExternalMessageAction(string id, string action, string data)
			{
				OnMessageAction?.Invoke(this, (id, action, data));
			}
		}

		byte[] GetChatResource(string name)
		{
			byte[] data;
			if (name == "prism.js")
			{
				// Fix compatibility for WebView2 control (Edge Chromium).
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
			return data;
		}

		public event EventHandler WebBrowserDataLoaded;

		#region Script Handler

		bool ScriptHandlerInitialized;

		#endregion

		#region HTML

		public async Task<object> InvokeScriptAsync(string script)
		{
			if (!ScriptHandlerInitialized)
				return default;

			try
			{
				// Wrap the script in a try-catch block
				string wrappedScript = $@"
                    (function() {{
                        try {{
                            return (function() {{
                                {script}
                            }})();
                        }} catch (e) {{
                            return 'Error:' + e.message + '\r\n' + e.stack;
                        }}
                    }})();
                ";

				// Execute the script asynchronously and get the result synchronously
				var result = await _WebView2.ExecuteScriptAsync(wrappedScript);
				return result;
			}
			catch (Exception ex)
			{
				// Handle exceptions that occur during script invocation
				Global.MainControl.InfoPanel.SetBodyError(ex.Message);
				Global.MainControl.ErrorsPanel.ErrorsLogPanel.Add(ex.ToString() + "\r\n");
			}
			return default;
		}

		#endregion

		private async void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			var isCtrlDown = Keyboard.Modifiers == ModifierKeys.Control;
			if (isCtrlDown && e.Key == Key.C)
			{
				bool isFocused = (bool)await InvokeScriptAsync("isElementFocused();");
				if (isFocused)
				{
					await InvokeScriptAsync("Copy();");
					e.Handled = true;
				}
			}
		}

		private async void KeyboardHook_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
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
			// Assuming the WebView2 control is hosted in the WPF window, you need to get its native handle.
			var window = Window.GetWindow(this);
			var helper = new System.Windows.Interop.WindowInteropHelper(window);
			IntPtr webView2Handle = _WebView2.Handle;
			if (webView2Handle == IntPtr.Zero)
				return;
			var focusedControl = NativeMethods.GetFocus();
			if (!IsAncestor(webView2Handle, focusedControl))
				return;
			// Works without the browser warning about access to clipboard.
			await InvokeScriptAsync("Copy();");
			e.Handled = true;
		}

		// Helper method to determine if webView2Handle is ancestor of focusedControl
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
