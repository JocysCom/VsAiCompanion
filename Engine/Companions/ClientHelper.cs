using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls.Chat;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;

namespace JocysCom.VS.AiCompanion.Engine.Companions
{
	public static class ClientHelper
	{

		public const string PreviewModeMessage = "Preview Mode - Sending messages to AI is suppressed.";
		public const string UserName = "User";
		public const string SystemName = "System";
		public const string AiName = "Ai";
		public const string GenerateTitleTaskName = "® System - Generate Title";
		public const string FormatMessageTaskName = "® System - Format Message";
		public const string PluginApprovalTaskName = "® System - Plugin Approval";
		public const string DefaultTaskItemIconEmbeddedResource = "document_gear.svg";
		public const string DefaultFineTuningIconEmbeddedResource = "control_panel.svg";
		public const string DefaultAssistantIconEmbeddedResource = "user_comment.svg";

		public static string JoinMessageParts(params string[] args)
		{
			return string.Join("\r\n\r\n", args.Where(x => !string.IsNullOrEmpty(x)));
		}

		public static string ConvertAttachmentsToString(params MessageAttachments[] attachments)
		{
			var s = "";
			foreach (var a in attachments)
			{
				s += $"\r\n\r\n{a.Title}:";
				if (!string.IsNullOrEmpty(a.Instructions))
					s += $"\r\n\r\n{a.Instructions}";
				s += $"\r\n\r\n{a.Data}";
				s = s.Trim('\r', '\n');
			}
			return s;
		}

		public static List<chat_completion_message> ConvertMessageItemToChatMessage(bool isSystemInstructions, MessageItem message, bool includeAttachments)
		{
			var completionMessages = new List<chat_completion_message>();
			var body = message.Body;
			if (includeAttachments && (message.Type == MessageType.In || message.Type == MessageType.Out))
				body = JoinMessageParts(body, ConvertAttachmentsToString(message.Attachments.ToArray()));
			if (message.Type == MessageType.In)
			{
				// Add AI assitant message.
				completionMessages.Add(new chat_completion_message(message_role.assistant, body));
				return completionMessages;
			}
			if (message.Type == MessageType.Out)
			{

				// Add system message.
				if (isSystemInstructions && !string.IsNullOrEmpty(message.BodyInstructions))
					completionMessages.Add(new chat_completion_message(message_role.system, message.BodyInstructions));
				// Add user message.
				var userContent = isSystemInstructions
					? body
					: JoinMessageParts(message.BodyInstructions, body);
				completionMessages.Add(new chat_completion_message(message_role.user, userContent));
			}
			return completionMessages;
		}

		public async static Task Send(TemplateItem item,
			Action executeBeforeAddMessage = null,
			string overrideText = null,
			MessageItem overrideMessage = null
			)
		{
			System.Diagnostics.Debug.WriteLine($"Send on Item: {item.Name}");
			if (!Global.IsGoodSettings(item.AiService, true))
				return;
			if (!Global.ValidateServiceAndModel(item))
				return;
			if (item.IsBusy)
				return;
			if (string.IsNullOrEmpty(item.AiModel))
			{
				Global.SetWithTimeout(MessageBoxImage.Warning, "Please select an AI model from the dropdown.");
				return;
			}
			MessageItem m;
			if (overrideMessage == null)
			{
				if (item.Messages == null)
					item.Messages = new BindingList<MessageItem>();
				// If task panel then allow to use AutoClear.
				var isTask = Global.Tasks.Items.Contains(item);
				// Message is added. Cleanup now.
				var itemText = overrideText ?? item.Text;
				if (isTask)
				{
					if (item.MessageBoxOperation == MessageBoxOperation.ClearMessage)
						item.Text = "";
					if (item.MessageBoxOperation == MessageBoxOperation.ResetMessage)
					{
						var template = Global.GetSettings(ItemType.Template).Items
							.Cast<TemplateItem>()
							.Where(x => x.Name == item.TemplateName)
							.FirstOrDefault();
						if (template != null)
							item.Text = template.Text;
					}
				}

				if (item.AutoFormatMessage)
					itemText = await FormatMessage(item, itemText);
				var vsData = AppHelper.GetMacroValues();
				// Prepare instructions.
				var instructions = item.TextInstructions;
				if (item.UseMacros)
					instructions = AppHelper.ReplaceMacros(instructions, vsData);
				m = new MessageItem(UserName, itemText, MessageType.Out);
				m.BodyInstructions = instructions;
				if (item.UseMacros)
					m.Body = AppHelper.ReplaceMacros(m.Body, vsData);
				var fileItems = new List<DocItem>();
				var at = item.AttachContext;
				// If data from clipboard.
				if (at.HasFlag(AttachmentType.Clipboard))
				{
					var clip = AppHelper.GetClipboard();
					var clipAttachment = new MessageAttachments()
					{
						Title = Global.AppSettings.ContextDataTitle,
						Type = item.AttachContext,
						Data = clip.Data,
					};
					m.Attachments.Add(clipAttachment);
				}
				// If text selection in Visual Studio.
				if (at.HasFlag(AttachmentType.Selection))
				{
					var ad = Global.GetSelection();
					var adAttachment = new MessageAttachments(AttachmentType.Selection, ad.Language, ad.Data);
					m.Attachments.Add(adAttachment);
				}
				// If selected error in Visual Studio.
				if (at.HasFlag(AttachmentType.Error))
				{
					var err = Global.GetSelectedError();
					if (!string.IsNullOrEmpty(err?.Description))
					{
						var errorAttachment = new MessageAttachments(AttachmentType.Error, err);
						m.Attachments.Add(errorAttachment);
					}
				}
				// If active open document in Visual Studio.
				if (at.HasFlag(AttachmentType.ActiveDocument))
				{
					var ad = Global.GetActiveDocument();
					var adAttachment = new MessageAttachments(AttachmentType.ActiveDocument, ad.Language, ad.Data);
					m.Attachments.Add(adAttachment);
				}
				if (at.HasFlag(AttachmentType.OpenDocuments))
					fileItems.AddRange(Global.GetOpenDocuments());
				if (at.HasFlag(AttachmentType.SelectedDocuments))
					fileItems.AddRange(Global.GetSelectedDocuments());
				if (at.HasFlag(AttachmentType.ActiveProject))
					fileItems.AddRange(Global.GetActiveProject());
				if (at.HasFlag(AttachmentType.SelectedProject))
					fileItems.AddRange(Global.GetSelectedProject());
				if (at.HasFlag(AttachmentType.Solution))
					fileItems.AddRange(Global.GetSolution());
				if (at.HasFlag(AttachmentType.ErrorDocument))
				{
					var doc = Global.GetSelectedErrorDocument();
					if (doc == null)
					{
						Global.SetWithTimeout(MessageBoxImage.Warning, "Please select an error in the Visual Studio Error List.");
						return;
					}
					else
					{
						fileItems.Add(doc);
					}
				}
				if (at.HasFlag(AttachmentType.Exception))
				{
					var ei = Global.GetCurrentException();
					if (!string.IsNullOrEmpty(ei?.Message))
					{
						var exceptionAttachment = new MessageAttachments(AttachmentType.Exception, ei);
						m.Attachments.Add(exceptionAttachment);
					}
				}
				if (at.HasFlag(AttachmentType.ExceptionDocuments))
				{
					// Get files for exception.
					var exceptionFiles = Global.GetCurrentExceptionDocuments();
					// Extract files if exception info was pasted manually inside the message.
					var messagePaths = AppHelper.ExtractFilePaths(itemText);
					var uniquePaths = messagePaths
						.Where(x => exceptionFiles.All(y => !x.Equals(y.FullName, StringComparison.OrdinalIgnoreCase)));
					var messageFiles = uniquePaths.Select(x => new DocItem(null, x)).ToList();
					fileItems.AddRange(exceptionFiles);
					fileItems.AddRange(messageFiles);
				}
				// Attach files as message attachments at the end.
				if (fileItems.Count > 0)
				{
					var a2 = new MessageAttachments()
					{
						Title = Global.AppSettings.ContextFileTitle,
						Type = item.AttachContext,
						Data = DocItem.ConvertFile(fileItems),
					};
					m.Attachments.Add(a2);
				}
				// Mark message as preview is preview.
				m.IsPreview = item.IsPreview;
				// ShowSensitiveDataWarning
				if (fileItems.Count > 0 && Global.AppSettings.ShowDocumentsAttachedWarning)
				{
					var lines = new List<string>();
					foreach (var fileItem in fileItems)
					{
						if (string.IsNullOrEmpty(fileItem.Data))
							continue;
						var word = AppHelper.ContainsSensitiveData(fileItem.Data);
						if (string.IsNullOrEmpty(word))
							continue;
						lines.Add($"Word '{word}' in File: {fileItem.FullName}\r\n");
					}
					if (lines.Count > 0)
					{
						var text = JoinMessageParts(
							"Possible sensitive data has been detected. Do you want to send these files to AI?",
							string.Join("\r\n", lines)
						);
						var caption = $"{Global.Info.Product} - Send Files";
						var result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning);
						if (result != MessageBoxResult.Yes)
							return;
					}
				}
				// ShowDocumentsAttachedWarning
				if (fileItems.Count > 0 && Global.AppSettings.ShowDocumentsAttachedWarning)
				{
					var files = fileItems.Select(x => x.FullName).ToList();
					var text = JoinMessageParts(
						"Do you want to send these files to AI?",
						string.Join("\r\n", files)
					);
					var caption = $"{Global.Info.Product} - Send Files";
					var result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning);
					if (result != MessageBoxResult.Yes)
						return;
				}
			}
			else
			{
				m = overrideMessage;
			}
			// Get current message with all attachments.
			var chatLogMessages = ConvertMessageItemToChatMessage(item.IsSystemInstructions, m, includeAttachments: true);
			// Prepare list of messages to send.
			if (item.AttachContext.HasFlag(AttachmentType.ChatHistory))
			{
				// Get tokens available.
				var tokensLeftForChatHistory = GetAvailableTokens(item, chatLogMessages, item.UseMaximumContext);
				var historyMessages = item.Messages
					// Exclude preview messages from the history.
					.Where(x => !x.IsPreview)
					.SelectMany(x => ConvertMessageItemToChatMessage(item.IsSystemInstructions, x, false)).ToList();
				var attachMessages = AppHelper.GetMessages(historyMessages, tokensLeftForChatHistory, ChatLogOptions);
				chatLogMessages = attachMessages.Concat(chatLogMessages).ToList();
				if (Client.IsTextCompletionMode(item.AiModel) && attachMessages.Count > 0)
				{
					// Create attachment.
					var a0 = new MessageAttachments();
					a0.Title = Global.AppSettings.ContextChatTitle;
					a0.Instructions = Global.AppSettings.ContextChatInstructions;
					a0.Type = AttachmentType.ChatHistory;
					var options = new JsonSerializerOptions();
					options.WriteIndented = true;
					var json = JsonSerializer.Serialize(attachMessages, ChatLogOptions);
					a0.Data = $"```json\r\n{json}\r\n```";
					a0.IsMarkdown = true;
					// Update messages.
					var message = ConvertMessageItemToChatMessage(false, m, includeAttachments: true);
					var content = JoinMessageParts(message[0].content, ConvertAttachmentsToString(a0));
					chatLogMessages.Clear();
					chatLogMessages.Add(new chat_completion_message(message_role.user, content));
				}
			}
			var maxTokens = Client.GetMaxInputTokens(item);
			// Add the message item to the message list once all the content is added.
			// Adding the message will trigger an event that serializes and adds this message to the Chat HTML page.
			executeBeforeAddMessage?.Invoke();
			item.Messages.Add(m);
			var msgTokens = CountTokens(chatLogMessages, ChatLogOptions);
			if (item.IsPreview)
			{
				var message = new MessageItem(SystemName, PreviewModeMessage);
				item.Messages.Add(message);
			}
			else if (maxTokens < msgTokens)
			{
				var message = new MessageItem(SystemName, $"Message is too big. Message Tokens: {msgTokens}, Maximum Tokens: {maxTokens}", MessageType.Error);
				item.Messages.Add(message);
			}
			else
			{
				try
				{
					if (item.AutoGenerateTitle)
					{
						item.AutoGenerateTitle = false;
						_ = GenerateTitle(item);
					}
					var client = new Companions.ChatGPT.Client(item.AiService);
					var maxInputTokens = Client.GetMaxInputTokens(item);
					// Send body and context data. Make sure it runs on NON-UI thread.
					var messageItems = await Task.Run(async () => await client.QueryAI(
						item.AiModel,
						chatLogMessages,
						item.Creativity,
						item,
						maxInputTokens
					)).ConfigureAwait(true);
					// If assistant message was received.
					var assistantMessage = messageItems.FirstOrDefault();
					if (assistantMessage != null)
					{
						item.Messages.Add(assistantMessage);
						// Automation.
						SetData(item, assistantMessage.Body);
					}
					// If auto-reply message was added then...
					var userMessage = messageItems.Skip(1).FirstOrDefault();
					if (userMessage != null)
					{
						await Send(item, overrideMessage: userMessage);
					}
				}
				catch (Exception ex)
				{
					var message = new MessageItem(SystemName, ex.Message, MessageType.Error);
					item.Messages.Add(message);
				}
			}
			// If item type task, then allow to do auto removal.
			if (Global.Tasks.Items.Contains(item) && item.AutoRemove)
				_ = Global.MainControl.Dispatcher.BeginInvoke(new Action(() => { _ = Global.Tasks.Items.Remove(item); }));
		}

		public static JsonSerializerOptions ChatLogOptions = new JsonSerializerOptions
		{
			WriteIndented = true,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
			// Serialize enums as string for AI to understand.
			Converters = { new JsonStringEnumConverter() }
		};

		public static int GetAvailableTokens(TemplateItem item, List<chat_completion_message> messages = null, bool useMaximumContext = false)
		{
			var maxTokens = Client.GetMaxInputTokens(item);
			// Split 50%/50% between request and response.
			var maxRequesTokens = useMaximumContext
				? maxTokens
				: maxTokens / 2;
			var usedTokens = CountTokens(messages, ChatLogOptions);
			var availableTokens = maxRequesTokens - usedTokens;
			return availableTokens;
		}

		#region Reserved Tempalte Functions

		public async static Task<string> FormatMessage(TemplateItem item, string text)
		{
			if (string.IsNullOrEmpty((text ?? "").Trim()))
				return text;
			/// Try to get reserved template to generate title.
			var rItem = Global.Templates.Items.FirstOrDefault(x => x.Name == FormatMessageTaskName);
			if (rItem == null)
				return text;
			var messages = new List<chat_completion_message>();
			// Crate a copy in order not to add to existing list.
			try
			{
				// Add instructions to generate title to existing messages.
				messages.Add(new chat_completion_message(message_role.system, rItem.TextInstructions));
				// Supply data for processing.
				messages.Add(new chat_completion_message(message_role.user, text));
				var client = new Companions.ChatGPT.Client(item.AiService);
				var maxInputTokens = Client.GetMaxInputTokens(rItem);
				// Send body and context data. Make sure it runs on NON-UI thread.
				var messageItem = await Task.Run(async () => await client.QueryAI(
					rItem.AiModel,
					messages,
					rItem.Creativity,
					item,
					maxInputTokens
				)).ConfigureAwait(true);
				return messageItem.FirstOrDefault()?.Body ?? text;
			}
			catch (Exception ex)
			{
				var message = new MessageItem(SystemName, ex.Message, MessageType.Error);
				item.Messages.Add(message);
				return text;
			}
		}

		public async static Task GenerateTitle(TemplateItem item)
		{
			/// Try to get reserved template to generate title.
			var rItem = Global.Templates.Items.FirstOrDefault(x => x.Name == GenerateTitleTaskName);
			if (rItem == null)
				return;
			if (item.Messages.Count == 0)
				return;
			var availableTokens = GetAvailableTokens(item, null);
			var allmessages = item.Messages
				// Exclude preview messages from the history.
				//.Where(x => !x.IsPreview)
				.SelectMany(x => ConvertMessageItemToChatMessage(item.IsSystemInstructions, x, false)).ToList();
			var messages = AppHelper.GetMessages(allmessages, availableTokens, ChatLogOptions);
			// Crate a copy in order not to add to existing list.
			try
			{
				// Add instructions to generate title to existing messages.
				messages.Add(new chat_completion_message(message_role.system, rItem.TextInstructions));
				var client = new Companions.ChatGPT.Client(item.AiService);
				var maxInputTokens = Client.GetMaxInputTokens(rItem);
				// Send body and context data. Make sure it runs on NON-UI thread.
				var response = await Task.Run(async () => await client.QueryAI(
					rItem.AiModel,
					messages,
					rItem.Creativity,
					item,
					maxInputTokens
				)).ConfigureAwait(true);
				var body = response.FirstOrDefault()?.Body;
				if (!string.IsNullOrEmpty(body))
				{
					body = SettingsData<object>.RemoveInvalidFileNameChars(body);
					if (body.Split().Length > 0)
					{
						var title = string.Join(" ", body.Split().Take(6).ToList());
						if (Global.Tasks.Items.Contains(item))
							Global.Tasks.RenameItem(item, title);
					}
				}
			}
			catch (Exception ex)
			{
				var message = new MessageItem(SystemName, ex.Message, MessageType.Error);
				item.Messages.Add(message);
			}
		}

		#endregion

		/// <summary>
		/// Set data to Visual Studio.
		/// </summary>
		/// <param name="item"></param>
		/// <param name="data"></param>
		public static void SetData(TemplateItem item, string data)
		{
			if (item.AttachContext == AttachmentType.Selection && Global.SetSelection != null)
			{
				var vsData = AppHelper.GetMacroValues();
				var code = AppHelper.GetCodeFromReply(data);
				if (item.AutoOperation == DataOperation.Replace)
					Global.SetSelection(code);
				if (item.AutoOperation == DataOperation.InsertBefore)
					Global.SetSelection(code + vsData.Selection.Data);
				if (item.AutoOperation == DataOperation.InsertAfter)
					Global.SetSelection(vsData.Selection.Data + code);
				if (item.AutoFormatCode)
					Global.EditFormatSelection();
			}
			else if (item.AttachContext == AttachmentType.ActiveDocument && Global.SetActiveDocument != null)
			{
				var vsData = AppHelper.GetMacroValues();
				var code = AppHelper.GetCodeFromReply(data);
				if (item.AutoOperation == DataOperation.Replace)
					Global.SetActiveDocument(code);
				if (item.AutoOperation == DataOperation.InsertBefore)
					Global.SetActiveDocument(code + vsData.Selection.Data);
				if (item.AutoOperation == DataOperation.InsertAfter)
					Global.SetActiveDocument(vsData.Selection.Data + code);
				if (item.AutoFormatCode)
					Global.EditFormatDocument();
			}
		}

		public static int CountTokens(object item, JsonSerializerOptions options)
		{
			var json = JsonSerializer.Serialize(item, options);
			return CountTokens(json);
		}

		public static int CountTokens(string s)
		{
			int count = 0;
			bool inWord = false;
			for (int i = 0; i < s.Length; i++)
			{
				char c = s[i];
				char nextC = i < s.Length - 1 ? s[i + 1] : '\0';
				if (char.IsWhiteSpace(c) || char.IsPunctuation(c))
				{
					if (inWord)
					{
						count++;
						inWord = false;
					}
					if (!char.IsWhiteSpace(c))
					{
						if (c == '-' && char.IsLetter(nextC)) // don't split hyphenated words
							continue;
						// don't split contractions and handle multi-character punctuation
						if (c == '\'' && char.IsLetter(nextC) || c == nextC)
							i++;  // skip next character
						count++; // punctuation is a separate token
					}
				}
				else if (!inWord)
				{
					// start of a new word
					inWord = true;
				}
			}
			if (inWord)
				count++;  // count the last word if the string doesn't end with a punctuation or a whitespace
			return count;
		}

	}
}
