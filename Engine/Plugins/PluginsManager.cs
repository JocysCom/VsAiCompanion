using Azure.AI.OpenAI;
using JocysCom.VS.AiCompanion.Engine.Companions;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace JocysCom.VS.AiCompanion.Engine.Plugins
{
	/// <summary>
	/// TODO: use controllers and automatically read plugin info from OpenAPI specification.
	/// </summary>
	public class PluginsManager
	{

		#region Manage Functions

		/// <summary>
		/// Store method names with method info.
		/// </summary>
		public static Dictionary<string, System.Reflection.MethodInfo> PluginFunctions = new Dictionary<string, System.Reflection.MethodInfo>();

		/// <summary>
		/// Add all methods of the type.
		/// </summary>
		private static void AddMethods(Type type)
		{
			var bindingFlags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
			var methods = type.GetMethods(bindingFlags);
			foreach (var method in methods)
				PluginFunctions.Add(method.Name, method);
		}

		/// <summary>
		///  Register plugins.
		/// </summary>
		public static void RegisterPluginFunctions()
		{
			AddMethods(typeof(AiCompanion.Plugins.LinkReader.LinkReaderHelper));
			AddMethods(typeof(AiCompanion.Plugins.PowerShellExecutor.PowerShellExecutorHelper));
			AddMethods(typeof(JocysCom.VS.AiCompanion.Engine.Plugins.AutoContinueHelper));
		}

		/// <summary>
		/// Load Microsoft.NET.Sdk.Web libraries and include all public API methods from classes tagged with the[ApiController] attribute.
		/// </summary>
		/// <param name="path">Path to the folder with DLLs.</param>
		public static void LoadPluginFunctions(string path)
		{
			var dllFiles = Directory.GetFiles(path, "*.dll");
			foreach (var dllFile in dllFiles)
			{
				Assembly assembly;
				try
				{
					assembly = Assembly.LoadFrom(dllFile);
				}
				catch (Exception ex)
				{
					// Handle or log exceptions such as bad format, file not found, etc.
					Console.WriteLine($"Could not load assembly {dllFile}: {ex.Message}");
					continue;
				}
				// Assuming you only want classes directly tagged with [ApiController]
				// Adjust BindingFlags if you need to search for non-public types etc.
				var controllerTypes = assembly.GetTypes();
				//.Where(type => type.GetCustomAttributes<Controller>().Any());

				foreach (var type in controllerTypes)
				{
					AddMethods(type); // Reuse your existing method to add methods of the type
				}
			}
		}


		public static async Task<bool> ApproveExecution(TemplateItem item, string json)
		{
			if (item.PluginApprovalProcess == ToolCallApprovalProcess.DenyAll)
				return false;
			if (item.PluginApprovalProcess == ToolCallApprovalProcess.AllowAll)
				return true;
			var assistantApproved = false;
			string assistantEvaluation = null;
			if (item.PluginApprovalProcess == ToolCallApprovalProcess.UserWhenAssitantDenies || item.PluginApprovalProcess == ToolCallApprovalProcess.Assistant)
			{
				assistantEvaluation = await ClientHelper.EvaluateToolExecutionSafety(item) ?? "";
				Global.MainControl.Dispatcher.Invoke(() =>
				{
					var lastMessage = item.Messages.Last();
					var attachment = new ClassLibrary.Controls.Chat.MessageAttachments(AttachmentType.None, "text", assistantEvaluation);
					attachment.Title = "Approval by Secondary AI";
					attachment.IsAlwaysIncluded = true;
					lastMessage.Attachments.Add(attachment);
				});
				assistantApproved = assistantEvaluation.ToLower().Contains("function call approved");
				// If approval relies on AI assistan only then return result.
				if (item.PluginApprovalProcess == ToolCallApprovalProcess.Assistant)
					return assistantApproved;
			}
			if (item.PluginApprovalProcess == ToolCallApprovalProcess.User || item.PluginApprovalProcess == ToolCallApprovalProcess.UserWhenAssitantDenies)
			{
				// If assitant approved then return true.
				if (item.PluginApprovalProcess == ToolCallApprovalProcess.UserWhenAssitantDenies && assistantApproved)
					return true;
				// It is up to user now to approve.
				var text = "Do you want to execute function submitted by AI?";
				if (!string.IsNullOrEmpty(assistantEvaluation))
					text += assistantEvaluation;
				text += "\r\n\r\n" + json;
				var caption = $"{Global.Info.Product} - Plugin Function Approval";
				var result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
				return result == MessageBoxResult.Yes;
			}
			return false;
		}

		/// <summary>
		/// Call functions requester by OpenAI API.
		/// </summary>
		/// <param name="item">User settings.</param>
		/// <param name="json">function as JSON</param>
		public static async Task<string> ProcessPlugins(TemplateItem item, string json)
		{
			if (!item.PluginsEnabled)
				return null;
			if (!await ApproveExecution(item, json))
				return null;
			lock (PluginFunctions)
			{
				if (!PluginFunctions.Any())
					RegisterPluginFunctions();
			}
			var function = Client.Deserialize<chat_completion_function>(json);
			// Assuming the parameters JSON is a single string. Adjust if the structure is different.
			var parameter = function.parameters.additional_properties.FirstOrDefault().Value.GetString();
			if (parameter == null)
				return null;
			if (PluginFunctions.TryGetValue(function.name, out System.Reflection.MethodInfo methodInfo))
			{
				object classInstance = null;
				// If the method is not static, create an instance of the class.
				if (!methodInfo.IsStatic)
				{
					classInstance = Activator.CreateInstance(methodInfo.DeclaringType);
				}
				// Check if the method is asynchronous (returns a Task or Task<string>)
				if (typeof(Task).IsAssignableFrom(methodInfo.ReturnType))
				{
					// It's an async method. Await the task.
					var task = (Task)methodInfo.Invoke(classInstance, new object[] { parameter });
					await task.ConfigureAwait(false); // Ensure you await the task
													  // If the method returns a Task<string>, get the result.
					if (task is Task<string> stringTask)
					{
						var result = await stringTask;
						// Assuming you want to do something with the result here...
						//MessageBox.Show(result, "Execution Results", MessageBoxButton.OK, MessageBoxImage.Information);
						//Console.WriteLine(result);
						return result;
					}
				}
				else
				{
					// It's a synchronous method.
					var result = (string)methodInfo.Invoke(classInstance, new object[] { parameter });
					// Assuming you want to do something with the result here...
					//MessageBox.Show(result, "Execution Results", MessageBoxButton.OK, MessageBoxImage.Information);
					//Console.WriteLine(result);
					return result;
				}
			}
			else
			{
				// Handle the case where the methodInfo is not found for the given functionName
				MessageBox.Show($"The function '{function.name}' was not found.", "Execution Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			return null;
		}

		#endregion

		#region Microsoft's Reinvention of the Wheel

		/// <summary>
		/// Add completion tools to chat completion message request for OpenAI GPT.
		/// </summary>
		/// <param name="item">Template item with settings.</param>
		/// <param name="options">Chat completion options</param>
		public static void ProvideTools(TemplateItem item, ChatCompletionsOptions options)
		{
			if (!item.PluginsEnabled)
				return;
			// Define GetWebPageContents.
			options.ToolChoice = ChatCompletionsToolChoice.Auto;
			var getWebPageContentsFunction = new FunctionDefinition()
			{
				Name = "GetWebPageContents",
				Description = "Use to retrieve content of websites by URL.",
				// Define the JSON Schema for the function parameter
				Parameters = BinaryData.FromObjectAsJson(new
				{
					type = "object", // Since you are describing a function parameter, use 'object'
					properties = new
					{
						url = new
						{
							type = "string",
							description = "URL which points to the resource."
						}
					},
					// Define required parameters.
					required = new[] { "url" }
				})
			};
			var getWebPageContentsTool = new ChatCompletionsFunctionToolDefinition(getWebPageContentsFunction);
			options.Tools.Add(getWebPageContentsTool);
			// Defining RunPowerShellScript tool
			var runPowerShellScriptFunction = new FunctionDefinition
			{
				Name = nameof(AiCompanion.Plugins.PowerShellExecutor.PowerShellExecutorHelper.RunPowerShellScript),
				Description = "Execute a PowerShell script on user computer.",
				// Define the JSON Schema for the function parameter
				Parameters = BinaryData.FromObjectAsJson(new
				{
					type = "object", // Since you are describing a function parameter, use 'object'
					properties = new
					{
						script = new
						{
							type = "string",
							description = "PowerShell script to execute"
						}
					},
					// Define required parameters.
					required = new[] { "string" }
				})
			};
			var runPowerShellScriptTool = new ChatCompletionsFunctionToolDefinition(runPowerShellScriptFunction);
			options.Tools.Add(runPowerShellScriptTool);
		}

		#endregion

		#region Classic API

		/// <summary>
		/// Add completion tools to chat completion message request for OpenAI GPT.
		/// </summary>
		/// <param name="item">Template item with settings.</param>
		/// <param name="request">Chat completion request</param>
		public static void ProvideTools(TemplateItem item, chat_completion_request request)
		{
			if (!item.PluginsEnabled)
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
			if (!item.PluginsEnabled)
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

		#endregion

	}
}
