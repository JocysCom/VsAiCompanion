using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Controls.Chat;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	/// <summary>
	/// AI execution logic for query processing and model management.
	/// </summary>
	public partial class Client
	{
		#region Main AI Query Methods

		/// <summary>
		/// Query AI - Main entry point for AI interactions.
		/// </summary>
		/// <param name="item">Item that will be affected: Used for insert/remove HttpClients.</param>
		/// <param name="messagesToSend">Messages to send to the AI.</param>
		/// <param name="embeddingText">Text for embedding context.</param>
		/// <returns>List of message items containing the AI response.</returns>
		public async Task<List<MessageItem>> QueryAI(
			TemplateItem item,
			List<chat_completion_message> messagesToSend,
			string embeddingText
		)
		{
			// Service item.
			var service = item.AiService;
			var modelName = item.AiModel;
			var aiModel = Global.AiModels.Items.FirstOrDefault(x => x.AiServiceId == service.Id && x.Name == modelName);
			
			// Determine which endpoint type to use
			var endpointType = aiModel?.EndpointType ?? AiModelEndpointType.Auto;
			
			switch (endpointType)
			{
				case AiModelEndpointType.OpenAI_Chat:
					return await QueryAI_ChatCompletion(item, messagesToSend, embeddingText);
					
				case AiModelEndpointType.OpenAI_Response:
					return await QueryAI_Response(item, messagesToSend, embeddingText);
					
				case AiModelEndpointType.Auto:
					// Auto-detect based on model name
					if (IsResponseModel(modelName))
						return await QueryAI_Response(item, messagesToSend, embeddingText);
					else
						return await QueryAI_ChatCompletion(item, messagesToSend, embeddingText);
					
				default:
					return await QueryAI_ChatCompletion(item, messagesToSend, embeddingText); // Safe fallback
			}
		}

		/// <summary>
		/// Query AI using Chat Completion endpoint (existing logic)
		/// </summary>
		private async Task<List<MessageItem>> QueryAI_ChatCompletion(
			TemplateItem item,
			List<chat_completion_message> messagesToSend,
			string embeddingText
		)
		{
			// Service item.
			var service = item.AiService;
			var modelName = item.AiModel;
			var aiModel = Global.AiModels.Items.FirstOrDefault(x => x.AiServiceId == service.Id && x.Name == modelName);
			var maxInputTokens = GetMaxInputTokens(item);
			
			// Setup
			var (newMessageItems, functionResults, assistantMessageItem, cancellationTokenSource, id) = 
				SetupQueryExecution(item, service);
				
			var answer = "";
			try
			{
				var messages = PrepareMessages(messagesToSend);
				var completionsOptions = GetChatCompletionOptions(item);
				var (addToolsToOptions, addToolsToMessage) = SetupTools(item, aiModel, completionsOptions);
				
				var client = await GetAiClient(true, item);
				var chatClient = client.GetChatClient(modelName);

				var toolCalls = new List<ChatToolCall>();
				
				// Execute query with streaming or non-streaming
				if (service.ResponseStreaming && aiModel.HasFeature(AiModelFeatures.Streaming))
				{
					(answer, toolCalls) = await ExecuteChatStreamingAsync(
						chatClient, messages, completionsOptions, assistantMessageItem, cancellationTokenSource.Token);
				}
				else
				{
					(answer, toolCalls) = await ExecuteChatNonStreamingAsync(
						chatClient, messages, completionsOptions, cancellationTokenSource.Token);
				}

				// Process tools and functions
				var functions = ProcessToolsAndFunctions(addToolsToMessage, addToolsToOptions, answer, toolCalls);
				answer = functions.processedAnswer;
				
				// Get approval and process functions.
				await ProcessFunctions(item, functions.functions, functionResults, assistantMessageItem, cancellationTokenSource);
			}
			catch (Exception ex)
			{
				ExceptionDispatchInfo.Capture(ex).Throw();
			}
			finally
			{
				FinalizeQueryExecution(item, id, cancellationTokenSource);
			}
			
			return CompleteQueryExecution(item, newMessageItems, functionResults, assistantMessageItem, answer, cancellationTokenSource);
		}

		/// <summary>
		/// Query AI using Response endpoint (for o3-pro models)
		/// </summary>
		private async Task<List<MessageItem>> QueryAI_Response(
			TemplateItem item,
			List<chat_completion_message> messagesToSend,
			string embeddingText
		)
		{
			// Service item.
			var service = item.AiService;
			var modelName = item.AiModel;
			var aiModel = Global.AiModels.Items.FirstOrDefault(x => x.AiServiceId == service.Id && x.Name == modelName);
			
			// Setup
			var (newMessageItems, functionResults, assistantMessageItem, cancellationTokenSource, id) = 
				SetupQueryExecution(item, service);
				
			var answer = "";
			try
			{
				var messages = PrepareMessages(messagesToSend);
				var completionsOptions = GetChatCompletionOptions(item);
				var (addToolsToOptions, addToolsToMessage) = SetupTools(item, aiModel, completionsOptions);
				
				var client = await GetAiClient(true, item);
				var responsesClient = client.GetOpenAIResponseClient(modelName);

				var toolCalls = new List<ChatToolCall>();
				
				// Execute query with streaming or non-streaming
				if (service.ResponseStreaming && aiModel.HasFeature(AiModelFeatures.Streaming))
				{
					(answer, toolCalls) = await ExecuteResponseStreamingAsync(
						responsesClient, messages, completionsOptions, assistantMessageItem, cancellationTokenSource.Token);
				}
				else
				{
					(answer, toolCalls) = await ExecuteResponseNonStreamingAsync(
						responsesClient, messages, completionsOptions, cancellationTokenSource.Token);
				}

				// Process tools and functions
				var functions = ProcessToolsAndFunctions(addToolsToMessage, addToolsToOptions, answer, toolCalls);
				answer = functions.processedAnswer;
				
				// Get approval and process functions.
				await ProcessFunctions(item, functions.functions, functionResults, assistantMessageItem, cancellationTokenSource);
			}
			catch (Exception ex)
			{
				ExceptionDispatchInfo.Capture(ex).Throw();
			}
			finally
			{
				FinalizeQueryExecution(item, id, cancellationTokenSource);
			}
			
			return CompleteQueryExecution(item, newMessageItems, functionResults, assistantMessageItem, answer, cancellationTokenSource);
		}

		#endregion

		#region Model Detection and Configuration

		/// <summary>
		/// Check if model should use Response endpoint (for o3-pro models)
		/// </summary>
		private static bool IsResponseModel(string modelName)
		{
			if (string.IsNullOrEmpty(modelName))
				return false;
			
			var name = modelName.ToLowerInvariant();
			// Add more patterns as needed
			return name.Contains("o3-pro");
		}

		/// <summary>
		/// Gets the maximum input tokens for a template item.
		/// </summary>
		/// <param name="item">Template item to get max tokens for.</param>
		/// <returns>Maximum input tokens allowed.</returns>
		public static int GetMaxInputTokens(TemplateItem item)
		{
			var modelName = item.AiModel;
			// Try to get max input tokens value from the settings.
			var aiModel = Global.AiModels.Items.FirstOrDefault(x =>
				x.AiServiceId == item.AiServiceId && x.Name == item.AiModel);
			if (aiModel != null && aiModel.MaxInputTokens != 0)
				return aiModel.MaxInputTokens;
			return GetMaxInputTokens(modelName);
		}

		/// <summary>
		/// Gets the maximum input tokens for a specific model name.
		/// </summary>
		/// <param name="modelName">The model name to check.</param>
		/// <returns>Maximum input tokens for the model.</returns>
		public static int GetMaxInputTokens(string modelName)
		{
			// Autodetect.
			modelName = modelName.ToLowerInvariant();
			// Final o1 and o3 supports 200K tokens.
			if ((modelName.StartsWith("o1") || modelName.StartsWith("o3"))
				&& !modelName.Contains("o1-preview"))
				return 200 * 1000;
			// All GPT-4 preview models support 128K tokens (2024-01-28).
			if (modelName.Contains("-128k") ||
				modelName.StartsWith("o1") ||
				modelName.Contains("gpt-4o") ||
				modelName.Contains("grok") ||
				modelName.Contains("gemini") ||
				(modelName.Contains("gpt-4") && modelName.Contains("preview")))
				return 128 * 1000;
			if (modelName.Contains("-64k") || modelName.Contains("deepseek"))
				return 64 * 1024;
			if (modelName.Contains("-32k") || modelName.Contains("text-moderation"))
				return 32 * 1024;
			if (modelName.Contains("-16k") || modelName.Contains("gpt-3.5-turbo-1106"))
				return 16 * 1024;
			if (modelName.Contains("gpt-4") || modelName.Contains("text-embedding"))
				return 8192;
			if (modelName.Contains("gpt-3.5-turbo"))
				return 4096;
			if (modelName.Contains("gpt"))
				return 4097; // Default for gpt
			if (modelName.Contains("code-davinci-002"))
				return 8001;
			if (modelName.Contains("text-davinci-002") || modelName.Contains("text-davinci-003"))
				return 4097;
			if (modelName.Contains("ada") || modelName.Contains("babbage") || modelName.Contains("curie") || modelName.Contains("davinci"))
				return 2049; // Default for ada, babbage, curie, davinci
			if (modelName.Contains("code-cushman-001"))
				return 2048;
			return 128 * 1000; // Default for other models
		}

		/// <summary>
		/// Sets model features based on the model name and capabilities.
		/// </summary>
		/// <param name="item">AI model item to configure.</param>
		public static void SetModelFeatures(AiModel item)
		{
			if (item.Name.StartsWith("o1-preview"))
			{
				item.Features = AiModelFeatures.ChatSupport;
				item.IsFeaturesKnown = true;
			}
			else if (item.Name.StartsWith("o1"))
			{
				item.Features = AiModelFeatures.ChatSupport | AiModelFeatures.SystemMessages | AiModelFeatures.FunctionCalling;
				item.IsFeaturesKnown = true;
				item.Instructions = "Reply using a markdown code block for any code included.";
			}
			else if (item.Name.Contains("gemini"))
			{
				item.Features = AiModelFeatures.ChatSupport | AiModelFeatures.SystemMessages;
				item.IsFeaturesKnown = true;
			}
		}

		#endregion
	}
}
