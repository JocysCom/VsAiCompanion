using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Controls.Chat;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for TemplateItemControl.xaml
	/// </summary>
	public partial class TemplateItemControl : UserControl, INotifyPropertyChanged
	{
		public TemplateItemControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			AiCompanionComboBox.ItemsSource = Enum.GetValues(typeof(Companions.CompanionType));
			AiCompanionComboBox.SelectedItem = Companions.CompanionType.OpenAI;
			ChatPanel.OnSend += ChatPanel_OnSend;
			//SolutionRadioButton.IsEnabled = Global.GetSolutionDocuments != null;
			//ProjectRadioButton.IsEnabled = Global.GetProjectDocuments != null;
			//FileRadioButton.IsEnabled = Global.GetSelectedDocuments != null;
			//SelectionRadioButton.IsEnabled = Global.GetSelection != null;
			BindData();
			InitMacros();
			Global.OnSaveSettings += Global_OnSaveSettings;
		}

		private void Global_OnSaveSettings(object sender, EventArgs e)
		{
			// Update from previous settings.
			if (_item != null)
				_item.Settings = ChatPanel.MessagesPanel.GetWebSettings();
		}

		private async void ChatPanel_OnSend(object sender, EventArgs e)
		{
			await Send();
		}

		public async Task Send()
		{
			if (Global.IsIncompleteSettings())
				return;
			if (string.IsNullOrEmpty(_item.AiModel))
			{
				Global.MainControl.InfoPanel.SetWithTimeout(MessageBoxImage.Warning, "Please select an AI model from the dropdown.");
				return;
			}
			var m = new MessageItem()
			{
				BodyInstructions = ChatPanel.DataInstructionsTextBox.Text,
				Body = ChatPanel.DataTextBox.Text,
				User = "User",
				Type = MessageType.Out,
			};
			// If task panel then allow to use AutoClear.
			if (ItemControlType == ItemType.Task)
			{
				if (_item.MessageBoxOperation == MessageBoxOperation.ClearMessage)
					ChatPanel.DataTextBox.Text = "";
				if (_item.MessageBoxOperation == MessageBoxOperation.ResetMessage)
				{
					var template = Global.GetItems(ItemType.Template).Where(x => x.Name == _item.TemplateName).FirstOrDefault();
					if (template != null)
						ChatPanel.DataTextBox.Text = template.Text;
				}
			}
			var vsData = AppHelper.GetMacroValues();
			if (_item.UseMacros)
			{
				m.BodyInstructions = AppHelper.ReplaceMacros(m.BodyInstructions, vsData);
				m.Body = AppHelper.ReplaceMacros(m.Body, vsData);
			}
			DocItem di = null;
			List<DocItem> dis = null;
			ErrorItem err = null;
			switch (_item.AttachContext)
			{
				case AttachmentType.None:
					break;
				case AttachmentType.Clipboard:
					di = Global.GetClipboard();
					break;
				case AttachmentType.Selection:
					di = Global.GetSelection();
					break;
				case AttachmentType.ActiveDocument:
					di = Global.GetActiveDocument();
					break;
				case AttachmentType.SelectedDocuments:
					dis = Global.GetSelectedDocuments();
					break;
				case AttachmentType.ActiveProject:
					dis = Global.GetActiveProject();
					break;
				case AttachmentType.SelectedProject:
					dis = Global.GetSelectedProject();
					break;
				case AttachmentType.Solution:
					dis = Global.GetSolution();
					break;
				default:
					break;
			}
			if (_item.AttachError)
			{
				err = Global.GetSelectedError();
				if (!string.IsNullOrEmpty(err?.Description))
				{
					var a1 = new MessageAttachments();
					a1.Title = Global.AppSettings.ContextErrorTitle;
					a1.Type = AttachmentType.SelectedError;
					var options = new JsonSerializerOptions();
					options.WriteIndented = true;
					var json = JsonSerializer.Serialize(err, options);
					a1.Data = $"```json\r\n{json}\r\n```";
					m.Attachments.Add(a1);
				}
			}
			if (dis?.Count > 0)
			{
				var a2 = new MessageAttachments()
				{
					Title = Global.AppSettings.ContextFileTitle,
					Type = _item.AttachContext,
					Data = DocItem.ConvertFile(dis),
				};
				m.Attachments.Add(a2);
			}
			else if (!string.IsNullOrEmpty(di?.Data))
			{
				var a3 = new MessageAttachments()
				{
					Title = Global.AppSettings.ContextDataTitle,
					Type = _item.AttachContext,
					Data = _item.AttachContext == AttachmentType.Selection || _item.AttachContext == AttachmentType.ActiveDocument
					// Use markdown which will make AI to reply with markdown too.
					? $"```{di.Language}\r\n{di.Data}\r\n```"
					: di.Data,
				};
				m.Attachments.Add(a3);
			}
			var messageForAI = $"{m.BodyInstructions}\r\n\r\n{m.Body}";
			var maxTokens = Client.GetMaxTokens(_item.AiModel);
			var usedTokens = Client.CountTokens(messageForAI);
			// Split 50%/50% between request and response.
			var maxRequesTokens = maxTokens / 2;
			var reqTokens = Client.CountTokens(messageForAI);
			var availableTokens = maxRequesTokens - usedTokens;
			// Attach chat history at the end (use left tokens).
			if (_item.AttachChatHistory && _item.Messages?.Count > 0)
			{
				var a0 = new MessageAttachments();
				a0.Title = Global.AppSettings.ContextChatTitle;
				a0.Instructions = Global.AppSettings.ContextChatInstructions;
				a0.Type = AttachmentType.ChatHistory;
				var options = new JsonSerializerOptions();
				options.WriteIndented = true;
				if (_item.Messages == null)
					_item.Messages = new BindingList<MessageItem>();
				var messages = _item.Messages.Select(x => new MessageHistoryItem()
				{
					Date = x.Date,
					User = x.User,
					Body = $"{x.BodyInstructions}\r\n\r\n{x.Body}",
					Type = x.Type.ToString(),
				}).ToDictionary(x => x, x => 0);
				var keys = messages.Keys.ToArray();
				// Count number of tokens used by each message.
				foreach (var key in keys)
				{
					var messageJson = JsonSerializer.Serialize(messages[key], options);
					messages[key] = Client.CountTokens(messageJson);
				}
				var messagesToSend = AppHelper.GetMessages(messages, availableTokens);
				// Attach message body to the bottom of the chat instead.
				messageForAI = "";
				messagesToSend.Add(new MessageHistoryItem()
				{
					Date = m.Date,
					User = m.User,
					Body = $"{m.BodyInstructions}\r\n\r\n{m.Body}",
					Type = m.Type.ToString(),
				});
				var json = JsonSerializer.Serialize(messagesToSend, options);
				a0.Data = $"```json\r\n{json}\r\n```";
				m.Attachments.Add(a0);
			}
			foreach (var a in m.Attachments)
			{
				messageForAI += $"\r\n\r\n{a.Title}";
				if (!string.IsNullOrEmpty(a.Instructions))
					messageForAI += $"\r\n\r\n{a.Instructions}";
				messageForAI += $"\r\n\r\n{a.Data}";
				messageForAI = messageForAI.Trim('\r', '\n');
			}
			_item.Messages.Add(m);
			if (_item.IsPreview)
			{
				ChatPanel.MessagesPanel.AddMessage("System", PreviewModeMessage, MessageType.Information);
			}
			else
			{
				try
				{
					var client = new Companions.ChatGPT.Client(Global.AppSettings.OpenAiSettings.BaseUrl);
					// Send body and context data.
					var response = await client.QueryAI(_item.AiModel, messageForAI, _item.Creativity);
					if (response != null)
					{
						ChatPanel.MessagesPanel.AddMessage("AI", response, MessageType.In);
						if (_item.AttachContext == AttachmentType.Selection && Global.SetSelection != null)
						{
							var code = AppHelper.GetCodeFromReply(response);
							if (_item.AutoOperation == DataOperation.Replace)
								Global.SetSelection(code);
							if (_item.AutoOperation == DataOperation.InsertBefore)
								Global.SetSelection(code + vsData.Selection.Data);
							if (_item.AutoOperation == DataOperation.InsertAfter)
								Global.SetSelection(vsData.Selection.Data + code);
							if (_item.AutoFormatCode)
								Global.EditFormatSelection();
						}
						else if (_item.AttachContext == AttachmentType.ActiveDocument && Global.SetActiveDocument != null)
						{
							var code = AppHelper.GetCodeFromReply(response);
							if (_item.AutoOperation == DataOperation.Replace)
								Global.SetActiveDocument(code);
							if (_item.AutoOperation == DataOperation.InsertBefore)
								Global.SetActiveDocument(code + vsData.Selection.Data);
							if (_item.AutoOperation == DataOperation.InsertAfter)
								Global.SetActiveDocument(vsData.Selection.Data + code);
							if (_item.AutoFormatCode)
								Global.EditFormatDocument();
						}

					}
				}
				catch (Exception ex)
				{
					ChatPanel.MessagesPanel.AddMessage("System", ex.Message, MessageType.Error);
				}
			}
			// If item type task, then allow to do auto removal.
			if (ItemControlType == ItemType.Task && _item.AutoRemove && Global.Tasks.Items.Contains(_item))
			{
				_ = Dispatcher.BeginInvoke(new Action(() => { _ = Global.Tasks.Items.Remove(_item); }));
			}

		}

		public string CreativityName
		{
			get
			{
				var v = _item?.Creativity;
				if (v is null)
					return "";
				if (v >= 2 - 0.25)
					return "Very Creative";
				if (v >= 1.5 - 0.25)
					return "Creative";
				if (v >= 1 - 0.25)
					return "Balanced";
				if (v >= 0.5 - 0.25)
					return "Precise";
				return "Very Precise";
			}
		}

		public List<string> AiModels { get; set; } = new List<string>();

		public void UpdateAiModels(params string[] args)
		{
			// Make sure checkbox can display current model.
			var list = Global.AppSettings.OpenAiSettings.AiModels.ToList();
			foreach (var arg in args)
			{
				if (!string.IsNullOrEmpty(arg) && !list.Contains(arg))
					list.Add(arg);
			}
			AiModels = list;
			OnPropertyChanged(nameof(AiModels));
		}

		public DataOperation[] AutoOperations => (DataOperation[])Enum.GetValues(typeof(DataOperation));

		public Dictionary<AttachmentType, string> DataTypes
		{
			get
			{
				if (_DataTypes == null)
				{
					var values = new AttachmentType[] {
						AttachmentType.None,
						AttachmentType.Clipboard,
						AttachmentType.Selection,
						AttachmentType.ActiveDocument,
						AttachmentType.SelectedDocuments,
						AttachmentType.ActiveProject,
						AttachmentType.SelectedProject,
						AttachmentType.Solution,
					};
					_DataTypes = ControlsHelper.GetDictionary(values);
				}
				return _DataTypes;
			}
			set => _DataTypes = value;
		}
		Dictionary<AttachmentType, string> _DataTypes;



		public Dictionary<MessageBoxOperation, string> MessageBoxOperations
		{
			get
			{
				if (_DataTypes == null)
				{
					var values = (MessageBoxOperation[])Enum.GetValues(typeof(MessageBoxOperation));
					_MessageBoxOperations = ControlsHelper.GetDictionary(values);
				}
				return _MessageBoxOperations;
			}
			set => _MessageBoxOperations = value;
		}
		Dictionary<MessageBoxOperation, string> _MessageBoxOperations;

		TemplateItem _item;
		public void BindData(TemplateItem item = null)
		{
			var oldItem = _item;
			// Update from previous settings.
			if (_item != null)
			{
				_item.Settings = ChatPanel.MessagesPanel.GetWebSettings();
				_item.PropertyChanged -= _item_PropertyChanged;
			}
			// Set new item.
			_item = item ?? new TemplateItem();
			// Make sure that even custom AiModel old and new item is available to select.
			UpdateAiModels(item?.AiModel, oldItem?.AiModel);
			DataContext = _item;
			_item.PropertyChanged += _item_PropertyChanged;
			OnPropertyChanged(nameof(CreativityName));
			// New item is bound. Make sure that custom AiModel only for the new item is available to select.
			UpdateAiModels(item?.AiModel);
			IconPanel.BindData(_item);
			ChatPanel.MessagesPanel.SetDataItems(_item.Messages, _item.Settings);
			// AutoSend once enabled then...
			if (ItemControlType == ItemType.Task && _item.AutoSend)
			{
				// Disable auto-send so that it won't trigger every time item is bound.
				_item.AutoSend = false;
				_ = Dispatcher.BeginInvoke(new Action(() =>
				{
					_ = Send();
				}));
			}
		}

		private void _item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(TemplateItem.Creativity))
				OnPropertyChanged(nameof(CreativityName));
		}

		private async void ModelRefreshButton_Click(object sender, RoutedEventArgs e)
		{
			if (Global.IsIncompleteSettings())
				return;
			Regex filterRx = null;
			try
			{
				filterRx = new Regex(Global.AppSettings.OpenAiSettings.ModelFilter);
			}
			catch { }
			var client = new Companions.ChatGPT.Client(Global.AppSettings.OpenAiSettings.BaseUrl);
			var models = await client.GetModels();
			var modelIds = models
				.OrderByDescending(x => x.Id)
				.Select(x => x.Id)
				.ToArray();
			if (filterRx != null)
				modelIds = modelIds.Where(x => filterRx.IsMatch(x)).ToArray();
			if (modelIds.Any())
			{
				Global.AppSettings.OpenAiSettings.AiModels = modelIds;
			}
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AiModels)));
		}

		private void ListToggleButton_Click(object sender, RoutedEventArgs e)
		{
			PanelSettings.IsListPanelVisible = !PanelSettings.IsListPanelVisible;
			UpdateListToggleButtonIcon();
		}

		#region ■ Properties

		[Category("Main"), DefaultValue(ItemType.None)]
		public ItemType ItemControlType
		{
			get => _ItemControlType;
			set
			{
				_ItemControlType = value;
				// Update panel settings.
				PanelSettings.PropertyChanged -= PanelSettings_PropertyChanged;
				PanelSettings = Global.AppSettings.GetTaskSettings(value);
				PanelSettings.PropertyChanged += PanelSettings_PropertyChanged;
				// Update the rest.
				UpdateBarToggleButtonIcon();
				UpdateListToggleButtonIcon();
				OnPropertyChanged(nameof(BarPanelVisibility));
				IsFavoriteCheckBox.Visibility = value == ItemType.Template ? Visibility.Visible : Visibility.Collapsed;
			}
		}
		private ItemType _ItemControlType;

		TaskSettings PanelSettings { get; set; } = new TaskSettings();

		private void PanelSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PanelSettings.IsBarPanelVisible))
			{
				OnPropertyChanged(nameof(BarPanelVisibility));
				UpdateBarToggleButtonIcon();
			}
		}

		public void UpdateListToggleButtonIcon()
		{
			var rt = new RotateTransform();
			rt.Angle = PanelSettings.IsListPanelVisible ? 0 : 180;
			ListToggleButton.RenderTransform = rt;
			ListToggleButton.RenderTransformOrigin = new Point(0.5, 0.5);
		}

		public Visibility BarPanelVisibility
			=> PanelSettings.IsBarPanelVisible ? Visibility.Visible : Visibility.Collapsed;

		private void BarToggleButton_Click(object sender, RoutedEventArgs e)
		{
			PanelSettings.IsBarPanelVisible = !PanelSettings.IsBarPanelVisible;
			UpdateListToggleButtonIcon();
		}

		public void UpdateBarToggleButtonIcon()
		{
			var rt = new RotateTransform();
			rt.Angle = PanelSettings.IsBarPanelVisible ? 90 : 270;
			BarToggleButton.RenderTransform = rt;
			BarToggleButton.RenderTransformOrigin = new Point(0.5, 0.5);
		}

		#endregion

		#region Macros

		private void PropertiesRefreshButton_Click(object sender, RoutedEventArgs e)
		{
			InitMacros();
		}


		void InitMacros()
		{
			AddKeys(SelectionComboBox, null, AppHelper.GetReplaceMacrosSelection());
			AddKeys(FileComboBox, null, AppHelper.GetReplaceMacrosDocument());
			AddKeys(DateComboBox, null, AppHelper.GetReplaceMacrosDate());
			AddKeys(VsMacrosComboBox, null, Global.GetEnvironmentProperties());
		}

		int alwaysSelectedIndex = -1;

		void AddKeys(ComboBox cb, string name, IEnumerable<PropertyItem> list)
		{
			var options = new List<PropertyItem>();
			//options.Insert(0, new PropertyItem() { Display = name });
			options.AddRange(list);
			cb.ItemsSource = options;
			cb.SelectedIndex = alwaysSelectedIndex;
			cb.SelectionChanged += ComboBox_SelectionChanged;
		}
		private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var cb = (ComboBox)sender;
			if (cb.SelectedIndex <= alwaysSelectedIndex)
				return;
			var item = (PropertyItem)cb.SelectedItem;
			cb.SelectedIndex = alwaysSelectedIndex;
			InsertText(ChatPanel.DataTextBox, "{" + item.Key + "}");
		}

		public static void InsertText(TextBox box, string s, bool activate = false)
		{
			// Check if we need to set the control active
			if (activate)
				box.Focus();
			// Save the current position of the cursor
			var cursorPosition = box.CaretIndex;
			// Check if there is a selected text to replace
			if (box.SelectionLength > 0)
			{
				// Replace the selected text
				box.SelectedText = s;
			}
			else
			{
				// Insert the text at the cursor position
				box.Text = box.Text.Insert(cursorPosition, s);
				// Set the cursor after the inserted text
				box.CaretIndex = cursorPosition + s.Length;
			}
		}

		#endregion

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));



		#endregion

		const string PreviewModeMessage = "Preview Mode - Sending messages to AI is suppressed.";

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.IsDesignMode(this))
				return;
			var head = "Caring for Your Sensitive Data";
			var body = "As you share files for AI processing, please remember not to include confidential, proprietary, or sensitive information.";
			Global.MainControl.InfoPanel.HelpProvider.Add(ContextTypeComboBox, head, body, MessageBoxImage.Warning);
			Global.MainControl.InfoPanel.HelpProvider.Add(ContextTypeLabel, head, body, MessageBoxImage.Warning);
			Global.MainControl.InfoPanel.HelpProvider.Add(AttachmentIcon, head, body, MessageBoxImage.Warning);
			if (!Global.IsVsExtesion)
			{
				Global.MainControl.InfoPanel.HelpProvider.Add(ErrorCheckBox, ErrorCheckBox.Content as string, Global.VsExtensionFeatureMessage);
				Global.MainControl.InfoPanel.HelpProvider.Add(FileComboBox, UseMacrosCheckBox.Content as string, Global.VsExtensionFeatureMessage);
				Global.MainControl.InfoPanel.HelpProvider.Add(SelectionComboBox, UseMacrosCheckBox.Content as string, Global.VsExtensionFeatureMessage);
				Global.MainControl.InfoPanel.HelpProvider.Add(AutomationVsLabel, AutomationVsLabel.Content as string, Global.VsExtensionFeatureMessage);
				Global.MainControl.InfoPanel.HelpProvider.Add(AutoOperationComboBox, AutomationVsLabel.Content as string, Global.VsExtensionFeatureMessage);
				Global.MainControl.InfoPanel.HelpProvider.Add(AutoFormatCodeCheckBox, AutomationVsLabel.Content as string, Global.VsExtensionFeatureMessage);
			}
			Global.MainControl.InfoPanel.HelpProvider.Add(ChatHistoryCheckBox, ChatHistoryCheckBox.Content as string,
				"The AI API doesn't store messages, so the chat log must be attached to each request in order to simulate a conversation.");
			Global.MainControl.InfoPanel.HelpProvider.Add(IsPreviewCheckBox, IsPreviewCheckBox.Content as string,
				PreviewModeMessage);
			Global.MainControl.InfoPanel.HelpProvider.Add(IsFavoriteCheckBox, IsFavoriteCheckBox.Content as string,
				"Display the template button in the toolbar for quick task creation.");
		}

	}
}
