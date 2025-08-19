using JocysCom.VS.AiCompanion.Engine.Controls.Chat;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
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
			// Autodetect - Order matters: Most specific patterns first!
			modelName = modelName.ToLowerInvariant();

			// === High-context models (1M+ tokens) ===
			// Gemini 1.5+ models support 1M+ context
			if (modelName.Contains("gemini-1.5") ||
				modelName.Contains("gemini-2.0") ||
				modelName.Contains("gemini-pro-1.5") ||
				(modelName.Contains("gemini") && modelName.Contains("1m")))
				return 1000 * 1000; // 1M tokens

			// Claude 3.5 Sonnet and other high-context Claude models
			if (modelName.Contains("claude-3.5") ||
				(modelName.Contains("claude") && modelName.Contains("200k")))
				return 200 * 1000;

			// === Very high-context models (400K+ tokens) ===
			// GPT-5 supports 400K tokens
			if (modelName.Contains("gpt-5"))
				return 400 * 1000;

			// === Medium-high context models (200K-300K tokens) ===
			// Grok-4 supports 256K tokens
			if (modelName.Contains("grok-4"))
				return 256 * 1000;

			// Final o1 and o3 models support 200K tokens (excluding o1-preview)
			if (modelName.StartsWith("o3") ||
				(modelName.StartsWith("o1") && !modelName.Contains("preview")))
				return 200 * 1000;

			// === Standard high-context models (128K tokens) ===
			// Explicit 128K models
			if (modelName.Contains("-128k"))
				return 128 * 1000;

			// o1-preview models
			if (modelName.Contains("o1-preview"))
				return 128 * 1000;

			// GPT-4o models
			if (modelName.Contains("gpt-4o"))
				return 128 * 1000;

			// GPT-4.1 models (newer generation, likely high context)
			if (modelName.Contains("gpt-4.1"))
				return 128 * 1000;

			// Other Grok models (not grok-4)
			if (modelName.Contains("grok"))
				return 128 * 1000;

			// Legacy Gemini models (pre-1.5)
			if (modelName.Contains("gemini"))
				return 128 * 1000;

			// GPT-4 with preview
			if (modelName.Contains("gpt-4") && modelName.Contains("preview"))
				return 128 * 1000;

			// === Medium context models (32K-64K tokens) ===
			if (modelName.Contains("-64k") || modelName.Contains("deepseek"))
				return 64 * 1024;

			if (modelName.Contains("-32k") || modelName.Contains("text-moderation"))
				return 32 * 1024;

			// === Lower context models (16K and below) ===
			if (modelName.Contains("-16k") || modelName.Contains("gpt-3.5-turbo-1106"))
				return 16 * 1024;

			// Standard GPT-4 models (8K)
			if (modelName.Contains("gpt-4"))
				return 8192;

			// Text embedding models
			if (modelName.Contains("text-embedding"))
				return 8192;

			// GPT-3.5-turbo models
			if (modelName.Contains("gpt-3.5-turbo"))
				return 4096;

			// === Legacy models ===
			if (modelName.Contains("code-davinci-002"))
				return 8001;

			if (modelName.Contains("text-davinci-002") || modelName.Contains("text-davinci-003"))
				return 4097;

			if (modelName.Contains("ada") || modelName.Contains("babbage") ||
				modelName.Contains("curie") || modelName.Contains("davinci"))
				return 2049;

			if (modelName.Contains("code-cushman-001"))
				return 2048;

			// Default for unknown models - use a reasonable high context size
			return 128 * 1000;
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
			else if (item.Name.Contains("o3-pro"))
			{
				// o3-pro models use Response endpoint and have advanced reasoning capabilities
				item.Features = AiModelFeatures.ChatSupport | AiModelFeatures.SystemMessages | AiModelFeatures.Streaming;
				item.IsFeaturesKnown = true;
				item.EndpointType = AiModelEndpointType.OpenAI_Response;
				item.Instructions = "Use your advanced reasoning capabilities to provide detailed, step-by-step analysis.";
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
