using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System;
using JocysCom.ClassLibrary.Controls.Chat;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;

namespace JocysCom.VS.AiCompanion.Engine.Companions
{
	public static class ClientHelper
	{

		public const string PreviewModeMessage = "Preview Mode - Sending messages to AI is suppressed.";

		public async static Task Send(TemplateItem item)
		{
			if (Global.IsIncompleteSettings())
				return;
			if (string.IsNullOrEmpty(item.AiModel))
			{
				Global.MainControl.InfoPanel.SetWithTimeout(MessageBoxImage.Warning, "Please select an AI model from the dropdown.");
				return;
			}
			var m = new MessageItem("User", item.Text, MessageType.Out);
			m.BodyInstructions = item.TextInstructions;
			// If task panel then allow to use AutoClear.
			var isTask = Global.Tasks.Items.Contains(item);
			var vsData = AppHelper.GetMacroValues();
			if (item.UseMacros)
			{
				m.BodyInstructions = AppHelper.ReplaceMacros(m.BodyInstructions, vsData);
				m.Body = AppHelper.ReplaceMacros(m.Body, vsData);
			}
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
			if (at.HasFlag(AttachmentType.SelectedDocuments))
				fileItems.AddRange(Global.GetSelectedDocuments());
			if (at.HasFlag(AttachmentType.ActiveProject))
				fileItems.AddRange(Global.GetActiveProject());
			if (at.HasFlag(AttachmentType.SelectedProject))
				fileItems.AddRange(Global.GetSelectedProject());
			if (at.HasFlag(AttachmentType.Solution))
				fileItems.AddRange(Global.GetSolution());
			if (at.HasFlag(AttachmentType.ErrorDocument))
				fileItems.Add(Global.GetSelectedErrorDocument());
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
				var messagePaths = AppHelper.ExtractFilePaths(item.Text);
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
			var messageForAI = $"{m.BodyInstructions}\r\n\r\n{m.Body}";
			var maxTokens = Client.GetMaxTokens(item.AiModel);
			var usedTokens = Client.CountTokens(messageForAI);
			// Split 50%/50% between request and response.
			var maxRequesTokens = maxTokens / 2;
			var reqTokens = Client.CountTokens(messageForAI);
			var availableTokens = maxRequesTokens - usedTokens;
			// Attach chat history at the end (use left tokens).
			if (item.AttachChatHistory && item.Messages?.Count > 0)
			{
				var a0 = new MessageAttachments();
				a0.Title = Global.AppSettings.ContextChatTitle;
				a0.Instructions = Global.AppSettings.ContextChatInstructions;
				a0.Type = AttachmentType.ChatHistory;
				var options = new JsonSerializerOptions();
				options.WriteIndented = true;
				if (item.Messages == null)
					item.Messages = new BindingList<MessageItem>();
				var messages = item.Messages.Select(x => new MessageHistoryItem()
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
			item.Messages.Add(m);
			// Message is added. Cleanup now.
			if (isTask)
			{
				if (item.MessageBoxOperation == MessageBoxOperation.ClearMessage)
					item.Text = "";
				if (item.MessageBoxOperation == MessageBoxOperation.ResetMessage)
				{
					var template = Global.GetItems(ItemType.Template).Where(x => x.Name == item.TemplateName).FirstOrDefault();
					if (template != null)
						item.Text = template.Text;
				}
			}
			if (item.IsPreview)
			{
				var message = new MessageItem("System", PreviewModeMessage);
				item.Messages.Add(message);
			}
			else
			{
				try
				{
					var client = new Companions.ChatGPT.Client(Global.AppSettings.OpenAiSettings.BaseUrl);
					// Send body and context data.
					var response = await client.QueryAI(item.AiModel, messageForAI, item.Creativity);
					if (response != null)
					{
						var message = new MessageItem("AI", response, MessageType.In);
						item.Messages.Add(message);
						if (item.AttachContext == AttachmentType.Selection && Global.SetSelection != null)
						{
							var code = AppHelper.GetCodeFromReply(response);
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
							var code = AppHelper.GetCodeFromReply(response);
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
				}
				catch (Exception ex)
				{
					var message = new MessageItem("System", ex.Message, MessageType.Error);
					item.Messages.Add(message);
				}
			}
			// If item type task, then allow to do auto removal.
			if (Global.Tasks.Items.Contains(item) && item.AutoRemove)
				_ = Global.MainControl.Dispatcher.BeginInvoke(new Action(() => { _ = Global.Tasks.Items.Remove(item); }));

		}


	}
}
