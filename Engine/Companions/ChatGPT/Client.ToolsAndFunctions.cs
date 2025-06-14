using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Controls.Chat;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	/// <summary>
	/// Tools and functions processing for AI interactions.
	/// </summary>
	public partial class Client
	{
		#region Chat Completion Options

		/// <summary>
		/// Creates ChatCompletionOptions with settings from the template item.
		/// </summary>
		/// <param name="item">Template item containing AI settings.</param>
		/// <returns>Configured ChatCompletionOptions.</returns>
		public static ChatCompletionOptions GetChatCompletionOptions(TemplateItem item)
		{
			var options = new ChatCompletionOptions();
			// If creativity not normal then add it.
			if (item.Creativity != 1)
				options.Temperature = (float)item.Creativity;
			if (item.MaxCompletionTokensEnabled)
				options.MaxOutputTokenCount = item.MaxCompletionTokens;
			//// Need to use reflection to set the Temperature property
			//// because the developers used unnecessary C# 9.0 features that won't work on .NET 4.8.
			//typeof(ChatCompletionOptions)
			//	.GetProperty(nameof(ChatCompletionOptions.Temperature), BindingFlags.Public | BindingFlags.Instance)
			//		?.SetValue(options, (float)item.Creativity, null);
			return options;
		}

		#endregion

		#region Tool Conversion Methods

		/// <summary>
		/// Get function definitions that will be serialized and provided to the AI.
		/// </summary>
		/// <param name="toolCalls">Chat tools to convert.</param>
		/// <returns>List of function definitions for the AI.</returns>
		public static List<chat_completion_function> ConvertChatToolsTo(IReadOnlyList<ChatTool> toolCalls)
		{
			var functions = new List<chat_completion_function>();
			if (toolCalls?.Any() != true)
				return functions;
			foreach (var toolCall in toolCalls)
			{
				var json = JsonSerializer.Serialize(toolCall);
				var parameters = new base_item();
				if (toolCall.FunctionParameters != null)
					parameters = JsonSerializer.Deserialize<base_item>(toolCall.FunctionParameters.ToString());
				var function = new chat_completion_function()
				{
					name = toolCall.FunctionName,
					description = toolCall.FunctionDescription,
					parameters = parameters,
				};
				functions.Add(function);
			}
			return functions;
		}

		/// <summary>
		/// Convert function calls returned by the assistant to completion functions.
		/// </summary>
		/// <param name="toolCalls">Tool calls from the AI response.</param>
		/// <returns>List of completion functions.</returns>
		public static List<chat_completion_function> ConvertChatToolCallsTo(IReadOnlyList<ChatToolCall> toolCalls)
		{
			var functions = new List<chat_completion_function>();
			if (toolCalls?.Any() != true)
				return functions;
			foreach (var toolCall in toolCalls)
			{
				var json = JsonSerializer.Serialize(toolCall);
				var parameters = new base_item();
				if (toolCall.FunctionArguments != null)
					parameters = JsonSerializer.Deserialize<base_item>(toolCall.FunctionArguments);
				var function = new chat_completion_function()
				{
					id = toolCall.Id,
					name = toolCall.FunctionName,
					parameters = parameters,
				};
				functions.Add(function);
			}
			return functions;
		}

		#endregion

		#region Function Processing

		/// <summary>
		/// Processes function calls from the AI and executes them.
		/// </summary>
		/// <param name="item">Template item for context.</param>
		/// <param name="functions">Functions to process.</param>
		/// <param name="functionResults">Output list for function results.</param>
		/// <param name="assistantMessageItem">Assistant message to update.</param>
		/// <param name="cancellationTokenSource">Cancellation token source.</param>
		public static async Task ProcessFunctions(
			TemplateItem item,
			List<chat_completion_function> functions,
			// Output parameters
			List<MessageAttachments> functionResults,
			MessageItem assistantMessageItem,
			CancellationTokenSource cancellationTokenSource
			)
		{
			if (functions?.Any() != true)
				return;
			var functionsList = functions.Select(f => new
			{
				f.id,
				f.name,
				parameters = PluginsManager.ConvertFromToolItem(PluginsManager.GetPluginFunctions().FirstOrDefault(x => x.Name == f.name)?.Mi, f)
			});

			// Serialize function calls as YAML for display as attachment to avoid confusing the AI.
			// Otherwise, it starts outputting JSON instead of calling functions.
			//var yaml = new SerializerBuilder().Build().Serialize(functionsList);
			var json = Serialize(functionsList, true);
			// Create message attachment first.
			//var fnCallAttachment = new MessageAttachments(ContextType.None, "YAML", yaml);
			var fnCallAttachment = new MessageAttachments(ContextType.None, "JSON", json);
			fnCallAttachment.Title = "Function Calls";
			// Don't send it back to AI or it will confuse it and it will start outputing YAML instead of calling functions.
			fnCallAttachment.SendType = AttachmentSendType.User;
			// Note: Maybe ask AI asistant to record call in its reply.
			// Add call to user message so that AI will see what functions it called.
			//var fnCallAttachmentUser = new MessageAttachments(ContextType.None, "YAML", yaml);
			//var fnCallAttachmentUser = new MessageAttachments(ContextType.None, "JSON", json);
			//fnCallAttachmentUser.Title = "Functions Call";
			//fnCallAttachmentUser.SendType = AttachmentSendType.User;
			//functionResults.Add(fnCallAttachmentUser);
			ControlsHelper.AppInvoke(() =>
			{
				assistantMessageItem.Attachments.Add(fnCallAttachment);
				assistantMessageItem.IsAutomated = true;
				var now = DateTime.Now;
				assistantMessageItem.Updated = now;
				item.Modified = now;
			});
			// Process function calls.
			if (item.PluginsEnabled)
			{
				foreach (var function in functions)
				{
					var content = await PluginsManager.ProcessPluginFunction(item, function, cancellationTokenSource);
					var fnResultAttachment = new MessageAttachments(ContextType.None, content.Value.Item1, content.Value.Item2);
					fnResultAttachment.Title = "Function Results (Id:" + function.id + ")";
					fnResultAttachment.SendType = AttachmentSendType.None;
					functionResults.Add(fnResultAttachment);
				}
			}
		}

		#endregion
	}
}
