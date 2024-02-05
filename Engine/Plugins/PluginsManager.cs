using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System.Collections.Generic;
using System.Text.Json;

namespace JocysCom.VS.AiCompanion.Engine.Plugins
{
	/// <summary>
	/// TODO: use controllers and automatically read plugin info from OpenAPI specification.
	/// </summary>
	public class PluginsManager
	{

		/// <summary>
		/// Add completion tools to chat completion message request for OpenAI GPT.
		/// </summary>
		/// <param name="item">Template item with settings.</param>
		/// <param name="request">Chat completion request</param>
		public static void ProvideTools(TemplateItem item, chat_completion_request request)
		{
			if (!item.UsePlugins)
				return;
			if (request.tools == null)
				request.tools = new List<chat_completion_tool>();
			// Define GetWebPageContents.
			var getWebPageContentsTool = new chat_completion_tool
			{
				type = chat_completion_tool_type.function,
				function = new chat_completion_function
				{
					description = "Use to retrieve content of websites by URL.",
					name = nameof(AiCompanion.Plugins.LinkReader.LinkReaderHelper.GetWebPageContents),
					parameters = new base_item
					{
						additional_properties = new Dictionary<string, JsonElement>
						{
							// Assuming the URL is a string. Adjust if necessary.
							["url"] = JsonSerializer.SerializeToElement("")
						}
					}
				}
			};
			request.tools.Add(getWebPageContentsTool);
			// Defining RunPowerShellScript tool
			var runPowerShellScriptTool = new chat_completion_tool
			{
				type = chat_completion_tool_type.function,
				function = new chat_completion_function
				{
					description = "Execute a PowerShell script.",
					name = nameof(AiCompanion.Plugins.PowerShellExecutor.PowerShellExecutorHelper.RunPowerShellScript),
					parameters = new base_item
					{
						additional_properties = new Dictionary<string, JsonElement>
						{
							// Assuming the script is a string. Adjust if necessary.
							["script"] = JsonSerializer.SerializeToElement("")
						}
					}
				}
			};
			request.tools.Add(runPowerShellScriptTool);
			// New AutoContinue tool setup
			var autoContinueTool = new chat_completion_tool
			{
				type = chat_completion_tool_type.function,
				function = new chat_completion_function
				{
					description = "Use it to ask user for permission to continue working on the task.",
					name = nameof(JocysCom.VS.AiCompanion.Engine.Plugins.AutoContinueHelper.AutoContinue),
					parameters = new base_item
					{
						additional_properties = new Dictionary<string, JsonElement>
						{
							// Assuming the message is a string. Adjust if necessary.
							["message"] = JsonSerializer.SerializeToElement("")
						}
					}
				}
			};
		}

		public static void ProcessPlugins(TemplateItem item, chat_completion_message message)
		{
			if (!item.UsePlugins)
				return;
			if (message.tool_calls == null)
				return;
			foreach (var toolCall in message.tool_calls)
			{
				if (toolCall.type == chat_completion_tool_type.function)
				{
					// Call the function
					var functionName = toolCall.function.name;
					var functionParams = toolCall.function.parameters;
					//https://platform.openai.com/docs/guides/function-calling?lang=node.js
					switch (functionName)
					{
						case nameof(AiCompanion.Plugins.LinkReader.LinkReaderHelper.GetWebPageContents):
							var url = toolCall.function.parameters.additional_properties["url"].Deserialize<string>();
							var content = AiCompanion.Plugins.LinkReader.LinkReaderHelper.GetWebPageContents(url);
							break;
						case nameof(AiCompanion.Plugins.PowerShellExecutor.PowerShellExecutorHelper.RunPowerShellScript):
							// Before running script analyse it with another AI and show evaluation results to the user with option to cancel.
							var script = toolCall.function.parameters.additional_properties["script"].Deserialize<string>();
							var output = AiCompanion.Plugins.PowerShellExecutor.PowerShellExecutorHelper.RunPowerShellScript(script);
							break;
						case nameof(JocysCom.VS.AiCompanion.Engine.Plugins.AutoContinueHelper.AutoContinue):
							var msg = toolCall.function.parameters.additional_properties["message"].Deserialize<string>();
							var response = JocysCom.VS.AiCompanion.Engine.Plugins.AutoContinueHelper.AutoContinue(msg);
							break;
						default:
							break;
					}

				}
			}
		}

	}
}
