using JocysCom.VS.AiCompanion.Engine;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace JocysCom.ClassLibrary.Controls.Chat
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

		public void SetDataItems(BindingList<MessageItem> messages, ChatSettings settings)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			if (Messages != null)
				Messages.ListChanged -= DataItems_ListChanged;
			Settings = settings;
			Messages = messages;
			Messages.ListChanged += DataItems_ListChanged;
			IsResetMessgesPending = !ScriptHandlerInitialized;
			if (ScriptHandlerInitialized)
				ResetWebMessages();
		}

		private ChatSettings Settings;
		private bool IsResetMessgesPending;

		private void ResetWebMessages()
		{
			InvokeScript($"DeleteMessages();");
			foreach (var message in Messages)
			{
				if (string.IsNullOrEmpty(message.Id))
					message.Id = Guid.NewGuid().ToString("N");
				InsertWebMessage(message, false);
			}
			SetWebSettings(Settings);
		}

		private void Tasks_ListChanged(object sender, ListChangedEventArgs e)
			=> UpdateUpdateButton();

		public void SetWebSettings(ChatSettings settings)
		{
			var json = JsonSerializer.Serialize(settings);
			InvokeScript($"SetSettings({json});");
		}

		public ChatSettings GetWebSettings()
		{
			if (!ScriptHandlerInitialized)
				return null;
			var json = InvokeScript($"GetSettings();");
			if (string.IsNullOrEmpty(json))
				return null;
			try
			{
				return (ChatSettings)JsonSerializer.Deserialize(json, typeof(ChatSettings));
			}
			catch (Exception ex)
			{
				Global.MainControl.InfoPanel.SetBodyError(ex.Message);
			}
			return null;
		}

		void InsertWebMessage(MessageItem item, bool autoScroll)
		{
			var json = JsonSerializer.Serialize(item);
			InvokeScript($"InsertMessage({json}, {autoScroll.ToString().ToLower()});");
		}

		private void DataItems_ListChanged(object sender, ListChangedEventArgs e)
		{
			if (e.ListChangedType == ListChangedType.ItemAdded)
				InsertWebMessage(Messages[e.NewIndex], true);
		}

		public BindingList<MessageItem> Messages { get; set; }

		private void MainDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
			=> UpdateUpdateButton();

		void UpdateUpdateButton()
		{
		}

		private void UserControl_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			// Allow to load once.
			if (ControlsHelper.AllowLoad(this))
			{
				WebBrowser.Navigating += WebBrowser_Navigating;
				WebBrowser.LoadCompleted += WebBrowser_LoadCompleted;
				WebBrowser.Navigate("http://localhost/ChatListControl.html");
			}
		}

		private void WebBrowser_Navigating(object sender, System.Windows.Navigation.NavigatingCancelEventArgs e)
		{
			if (e.Uri == null)
				return;
			var fileName = e.Uri.AbsolutePath;
			// Check if it's trying to navigate to one of our files.
			if (fileName.EndsWith("ChatListControl.html"))
			{
				var contents = GetResource("ChatListControl.html");
				LoadResource(ref contents, "IconInformation.svg");
				LoadResource(ref contents, "IconQuestion.svg");
				LoadResource(ref contents, "IconWarning.svg");
				LoadResource(ref contents, "IconError.svg");
				LoadResource(ref contents, "IconIn.svg");
				LoadResource(ref contents, "IconOut.svg");
				LoadResource(ref contents, "core.min.js");
				LoadResource(ref contents, "marked.min.js");
				LoadResource(ref contents, "prism.css");
				LoadResource(ref contents, "prism.js");
				WebBrowser.NavigateToString(contents);
				return;
			}
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
			contents = contents.Replace("ChatListControl/" + name, Files.Mime.GetResourceDataUri(name, data));
		}

		private void WebBrowser_LoadCompleted(object sender, System.Windows.Navigation.NavigationEventArgs e)
		{
			WebBrowser.ObjectForScripting = ScriptingHandler;
			// Add BeginInvoke to allow the JavaScript syntax highlighter to initialize after loading.
			Dispatcher.BeginInvoke(new Action(() =>
			{
				ScriptHandlerInitialized = true;
				// If messages set but messages are not loaded yet.
				if (IsResetMessgesPending)
					ResetWebMessages();
			}));
		}

		/// <summary>
		/// This handler will be triggered from ChatListControl.html
		/// </summary>
		private void _ScriptingHandler_OnMessageAction(object sender, string[] e)
		{
			var action = (MessageAction)Enum.Parse(typeof(MessageAction), e[1]);
			if (action == MessageAction.Loaded)
			{
				LoadingLabel.Visibility = Visibility.Collapsed;
				WebBrowser.Visibility = Visibility.Visible;
				return;
			}
			var id = e[0];
			var message = Messages.FirstOrDefault(x => x.Id == id);
			if (message == null)
				return;
			switch (action)
			{
				case MessageAction.Remove:
					Messages.Remove(message);
					InvokeScript($"DeleteMessage('{message.Id}');");
					break;
				case MessageAction.Copy:
					Clipboard.SetText(message.Body);
					break;
				default:
					break;
			}
		}

		#region Script Handler

		bool ScriptHandlerInitialized;

		public ScriptingHandler ScriptingHandler;

		#endregion

		public void AddMessage(string user, string body, MessageType type = MessageType.Information)
		{
			var message = new MessageItem()
			{
				Id = Guid.NewGuid().ToString("N"),
				Body = body,
				User = user,
				Type = type,
			};
			Messages.Add(message);
		}

		#region HTML

		public string InvokeScript(string script)
		{
			try
			{
				return (string)WebBrowser.InvokeScript("eval", new object[] { script });
			}
			catch (Exception ex)
			{
				Global.MainControl.InfoPanel.SetBodyError(ex.Message);
			}
			return null;
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

	}
}
