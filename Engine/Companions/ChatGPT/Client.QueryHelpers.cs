using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Controls.Chat;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	/// <summary>
	/// Helper methods for query execution, message preparation, and execution flow.
	/// </summary>
	public partial class Client
	{
		#region Query Execution Setup and Completion

		/// <summary>
		/// Setup common query execution parameters
		/// </summary>
		private (List<MessageItem> newMessageItems, List<MessageAttachments> functionResults, MessageItem assistantMessageItem, CancellationTokenSource cancellationTokenSource, Guid id) SetupQueryExecution(TemplateItem item, AiService service)
		{
			var newMessageItems = new List<MessageItem>();
			var functionResults = new List<MessageAttachments>();
			var cancellationTokenSource = new CancellationTokenSource();
			cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(service.ResponseTimeout));
			var id = Guid.NewGuid();
			var assistantMessageItem = new MessageItem(ClientHelper.AiName, "", MessageType.In);
			
			ControlsHelper.AppInvoke(() =>
			{
				item.CancellationTokenSources.Add(cancellationTokenSource);
				Global.MainControl.InfoPanel.AddTask(id);
				Global.AvatarPanel?.PlayMessageSentAnimation();
				newMessageItems.Add(assistantMessageItem);
				item.Messages.Add(assistantMessageItem);
				assistantMessageItem.Status = "Thinking";
			});
			
			return (newMessageItems, functionResults, assistantMessageItem, cancellationTokenSource, id);
		}

		/// <summary>
		/// Finalize query execution cleanup
		/// </summary>
		private void FinalizeQueryExecution(TemplateItem item, Guid id, CancellationTokenSource cancellationTokenSource)
		{
			ControlsHelper.AppInvoke(() =>
			{
				Global.MainControl.InfoPanel.RemoveTask(id);
				item.CancellationTokenSources.Remove(cancellationTokenSource);
				Global.AvatarPanel?.PlayMessageReceivedAnimation();
			});
			MessageDone?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Complete query execution and return results
		/// </summary>
		private List<MessageItem> CompleteQueryExecution(
			TemplateItem item,
			List<MessageItem> newMessageItems,
			List<MessageAttachments> functionResults,
			MessageItem assistantMessageItem,
			string answer,
			CancellationTokenSource cancellationTokenSource)
		{
			if (assistantMessageItem.Body != answer)
				assistantMessageItem.Body = answer;
			assistantMessageItem.Date = DateTime.Now;
			
			ControlsHelper.AppInvoke(() =>
			{
				assistantMessageItem.Updated = DateTime.Now;
				assistantMessageItem.Status = null;
			});
			
			if (!cancellationTokenSource.IsCancellationRequested && functionResults.Any())
			{
				var userAutoReplyMessageItem = new MessageItem(ClientHelper.UserName, "", MessageType.Out);
				foreach (var functionResult in functionResults)
					userAutoReplyMessageItem.Attachments.Add(functionResult);
				userAutoReplyMessageItem.IsAutomated = true;
				
				ControlsHelper.AppInvoke(() =>
				{
					newMessageItems.Add(userAutoReplyMessageItem);
					item.Messages.Add(userAutoReplyMessageItem);
				});
			}
			
			return newMessageItems;
		}

		#endregion

		#region Message Preparation

		/// <summary>
		/// Prepare messages from chat_completion_message to ChatMessage format
		/// </summary>
		private List<ChatMessage> PrepareMessages(List<chat_completion_message> messagesToSend)
		{
			var messages = new List<ChatMessage>();
			foreach (var messageToSend in messagesToSend)
			{
				var stringContent = messageToSend.content as string;
				ChatMessageContentPart[] contentItems = null;
				if (messageToSend.content is content_item[] citems)
					contentItems = citems.Select(x => ConvertToChatMessageContentItem(x)).ToArray();
				switch (messageToSend.role)
				{
					case message_role.user:
						if (contentItems != null)
							messages.Add(new UserChatMessage(contentItems));
						else if (!string.IsNullOrEmpty(stringContent))
							messages.Add(new UserChatMessage(stringContent));
						break;
					case message_role.assistant:
						if (!string.IsNullOrEmpty(stringContent))
							messages.Add(new AssistantChatMessage(stringContent));
						break;
					case message_role.system:
						if (!string.IsNullOrEmpty(stringContent))
							messages.Add(new SystemChatMessage(stringContent));
						break;
				}
			}
			return messages;
		}

		/// <summary>
		/// Setup tools for the query
		/// </summary>
		private (bool addToolsToOptions, bool addToolsToMessage) SetupTools(TemplateItem item, AiModel aiModel, ChatCompletionOptions completionsOptions)
		{
			var addToolsToOptions = item.PluginsEnabled && aiModel.HasFeature(AiModelFeatures.FunctionCalling);
			var addToolsToMessage = item.PluginsEnabled && !aiModel.HasFeature(AiModelFeatures.FunctionCalling);
			
			ControlsHelper.AppInvoke(() =>
			{
				if (addToolsToOptions)
				{
					var tools = PluginsManager.GetChatToolDefinitions(item);
					PluginsManager.ProvideTools(tools, item, options: completionsOptions);
				}
			});
			
			return (addToolsToOptions, addToolsToMessage);
		}

		#endregion

		#region Chat Execution Methods

		/// <summary>
		/// Execute chat completion with streaming
		/// </summary>
		private async Task<(string answer, List<ChatToolCall> toolCalls)> ExecuteChatStreamingAsync(
			ChatClient chatClient,
			List<ChatMessage> messages,
			ChatCompletionOptions completionsOptions,
			MessageItem assistantMessageItem,
			CancellationToken cancellationToken)
		{
			var answer = "";
			var toolCalls = new List<ChatToolCall>();
			var toolCallIdsByIndex = new Dictionary<int, string>();
			var functionNamesByIndex = new Dictionary<int, string>();
			var functionArgumentsByIndex = new Dictionary<int, MemoryStream>();

			var result = chatClient.CompleteChatStreamingAsync(messages, completionsOptions, cancellationToken);
			var choicesEnumerator = result.GetAsyncEnumerator(cancellationToken);
			
			try
			{
				while (await choicesEnumerator.MoveNextAsync().ConfigureAwait(false))
				{
					var choice = choicesEnumerator.Current;
					if (choice.ContentUpdate != null)
					{
						foreach (var cu in choice.ContentUpdate)
						{
							answer += cu.Text;
							ControlsHelper.AppInvoke(() =>
							{
								if (assistantMessageItem.Status != null)
									assistantMessageItem.Status = null;
								assistantMessageItem.AddToBodyBuffer(cu.Text);
							});
						}
					}
					if (choice.ToolCallUpdates != null)
					{
						foreach (StreamingChatToolCallUpdate update in choice.ToolCallUpdates)
						{
							var index = update.Index;
							if (!string.IsNullOrEmpty(update.ToolCallId))
								toolCallIdsByIndex[index] = update.ToolCallId;
							if (!string.IsNullOrEmpty(update.FunctionName))
								functionNamesByIndex[index] = update.FunctionName;
							if (update.FunctionArgumentsUpdate != null)
							{
								if (!functionArgumentsByIndex.TryGetValue(index, out MemoryStream stream))
								{
									stream = new MemoryStream();
									functionArgumentsByIndex[index] = stream;
								}
								using (Stream updateStream = update.FunctionArgumentsUpdate.ToStream())
									updateStream.CopyTo(stream);
							}
						}
					}
					await Task.Yield();
				}
			}
			finally
			{
				if (choicesEnumerator != null)
					await choicesEnumerator.DisposeAsync();
			}

			foreach (var kv in toolCallIdsByIndex)
			{
				var index = kv.Key;
				var toolCallId = kv.Value;
				var functionName = functionNamesByIndex[index];
				var stream = functionArgumentsByIndex[index];
				stream.Position = 0;
				var functionArguments = BinaryData.FromStream(stream);
				var toolCall = ChatToolCall.CreateFunctionToolCall(toolCallId, functionName, functionArguments);
				toolCalls.Add(toolCall);
			}

			return (answer, toolCalls);
		}

		/// <summary>
		/// Execute chat completion without streaming
		/// </summary>
		private async Task<(string answer, List<ChatToolCall> toolCalls)> ExecuteChatNonStreamingAsync(
			ChatClient chatClient,
			List<ChatMessage> messages,
			ChatCompletionOptions completionsOptions,
			CancellationToken cancellationToken)
		{
			var answer = "";
			var toolCalls = new List<ChatToolCall>();

			var result = await chatClient.CompleteChatAsync(messages, completionsOptions, cancellationToken);
			var completion = result.Value;
			
			switch (completion.FinishReason)
			{
				case ChatFinishReason.Stop:
				case ChatFinishReason.ToolCalls:
					answer = string.Join("\r\n", completion.Content?.Select(x => x.Text));
					break;
				case ChatFinishReason.Length:
					answer = "Incomplete model output due to MaxTokens parameter or token limit exceeded.";
					break;
				case ChatFinishReason.ContentFilter:
					answer = "Omitted content due to a content filter flag.";
					break;
				default:
					answer = result.ToString();
					break;
			}
			
			if (completion.ToolCalls?.Any() == true)
				toolCalls.AddRange(completion.ToolCalls);

			return (answer, toolCalls);
		}

		#endregion

		#region Response Execution Methods (for o3-pro models)

		/// <summary>
		/// Execute response with streaming (for o3-pro models)
		/// </summary>
		private Task<(string answer, List<ChatToolCall> toolCalls)> ExecuteResponseStreamingAsync(
			object responsesClient,
			List<ChatMessage> messages,
			ChatCompletionOptions completionsOptions,
			MessageItem assistantMessageItem,
			CancellationToken cancellationToken)
		{
			// TODO: Implement when OpenAI Response client API is available
			// This is a placeholder for future implementation
			// For now, fall back to non-streaming
			return ExecuteResponseNonStreamingAsync(responsesClient, messages, completionsOptions, cancellationToken);
		}

		/// <summary>
		/// Execute response without streaming (for o3-pro models)
		/// </summary>
		private Task<(string answer, List<ChatToolCall> toolCalls)> ExecuteResponseNonStreamingAsync(
			object responsesClient,
			List<ChatMessage> messages,
			ChatCompletionOptions completionsOptions,
			CancellationToken cancellationToken)
		{
			// TODO: Implement when OpenAI Response client API is available
			// This is a placeholder for future implementation
			var answer = "Response client not yet implemented for o3-pro models.";
			var toolCalls = new List<ChatToolCall>();
			return Task.FromResult((answer, toolCalls));
		}

		#endregion

		#region Tools and Functions Processing

		/// <summary>
		/// Process tools and functions
		/// </summary>
		private (string processedAnswer, List<chat_completion_function> functions) ProcessToolsAndFunctions(
			bool addToolsToMessage,
			bool addToolsToOptions,
			string answer,
			List<ChatToolCall> toolCalls)
		{
			List<chat_completion_function> functions = null;
			var processedAnswer = answer;
			
			if (addToolsToMessage)
			{
				var (assistantMessage, functionCalls) = PluginsManager.ProcessAssistantMessage(answer);
				if (functionCalls.Any())
				{
					processedAnswer = assistantMessage;
					functions = functionCalls.ToList();
				}
			}
			
			if (addToolsToOptions)
			{
				functions = ConvertChatToolCallsTo(toolCalls);
			}
			
			return (processedAnswer, functions);
		}

		#endregion
	}
}
