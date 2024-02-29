using Azure.AI.OpenAI;
using JocysCom.ClassLibrary.Xml;
using JocysCom.VS.AiCompanion.Engine.Companions;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using JocysCom.VS.AiCompanion.Plugins.Core;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace JocysCom.VS.AiCompanion.Engine
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
		public static Dictionary<string, System.Reflection.MethodInfo> PluginFunctions
		{
			get
			{
				lock (PluginFunctionsLock)
				{
					if (_PluginFunctions == null)
						_PluginFunctions = new Dictionary<string, System.Reflection.MethodInfo>();
					if (_PluginFunctions.Count == 0)
					{
						AddMethods(typeof(Basic));
						AddMethods(typeof(VisualStudio));
						AddMethods(typeof(Database));
						Search._databasePath = Global.PluginsSearchPath;
						AddMethods(typeof(Search));
#if DEBUG
						AddMethods(typeof(Lists));
						AddMethods(typeof(Automation));
#endif
					}
					return _PluginFunctions;
				}
			}
		}
		static Dictionary<string, System.Reflection.MethodInfo> _PluginFunctions;
		static object PluginFunctionsLock = new object();

		/// <summary>
		/// Add all methods of the type.
		/// </summary>
		private static void AddMethods(Type type)
		{
			var bindingFlags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
			var methods = type.GetMethods(bindingFlags);
			foreach (var method in methods)
				_PluginFunctions.Add(method.Name, method);
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

		public static bool AllowPlugin(string functionName, RiskLevel maxRiskLevel)
		{
			var currentPlugin = Global.AppSettings.Plugins.FirstOrDefault(x => x.Name == functionName);
			return currentPlugin?.IsEnabled == true && currentPlugin.RiskLevel <= maxRiskLevel;
		}

		/// <summary>
		/// Call functions requeste by OpenAI API.
		/// </summary>
		/// <param name="item">User settings.</param>
		/// <param name="json">function as JSON</param>
		public static async Task<string> ProcessPlugins(TemplateItem item, chat_completion_function function, CancellationTokenSource cancellationTokenSource)
		{
			if (!item.PluginsEnabled)
				return null;
			if (!AllowPlugin(function.name, item.MaxRiskLevel))
				return null;
			System.Reflection.MethodInfo methodInfo;
			if (!PluginFunctions.TryGetValue(function.name, out methodInfo))
			{
				// Handle the case where the methodInfo is not found for the given functionName
				MessageBox.Show($"The function '{function.name}' was not found.", "Execution Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return null;
			}
			if (item.PluginApprovalProcess == ToolCallApprovalProcess.DenyAll)
				return Resources.Resources.Call_function_request_denied;
			object classInstance = null;
			// If the method is not static, create an instance of the class.
			if (!methodInfo.IsStatic)
				classInstance = Activator.CreateInstance(methodInfo.DeclaringType);
			// Prepare an array of parameters for the method invocation.
			var methodParams = methodInfo.GetParameters();
			object[] invokeParams = new object[methodParams.Length];
			for (int i = 0; i < methodParams.Length; i++)
			{
				var param = methodParams[i];
				JsonElement jsonElement;
				// Extract parameters as a dictionary.
				var parameters = function.parameters.additional_properties;
				if (parameters == null)
					parameters = new Dictionary<string, JsonElement>();
				if (parameters.TryGetValue(param.Name, out jsonElement))
				{
					invokeParams[i] = jsonElement.Deserialize(param.ParameterType);
				}
				else if (param.HasDefaultValue)
				{
					invokeParams[i] = param.DefaultValue;
				}
				else
				{
					// Handle missing required parameter.
					MessageBox.Show($"The required parameter '{param.Name}' is missing for the function '{function.name}'.", "Execution Error", MessageBoxButton.OK, MessageBoxImage.Error);
					return null;
				}
			}

			var pfci = new PluginApprovalItem();
			Global.MainControl.Dispatcher.Invoke(() =>
			{
				pfci.Plugin = new PluginItem(methodInfo);
			});
			pfci.function = function;
			pfci.Args = invokeParams;

			var approved = await ApproveExecution(item, pfci, cancellationTokenSource);


			if (!approved)
				return Resources.Resources.Call_function_request_denied;

			// Check if the method is asynchronous (returns a Task or Task<string>)
			if (typeof(Task).IsAssignableFrom(methodInfo.ReturnType))
			{
				// It's an async method. Await the task.
				var task = (Task)methodInfo.Invoke(classInstance, invokeParams);
				await task.ConfigureAwait(false); // Ensure you await the task

				// If the method returns a Task<string>, get the result.
				if (task is Task<string> stringTask)
				{
					var result = await stringTask;
					return result;
				}
			}
			else
			{
				object methodResult = null;
				if (methodInfo.DeclaringType.Name == nameof(VisualStudio))
				{
					await Global.MainControl.Dispatcher.InvokeAsync(async () =>
					{
						await Global.SwitchToVisualStudioThreadAsync();
						methodResult = methodInfo.Invoke(classInstance, invokeParams);

					});
				}
				else
				{
					// It's a synchronous method.
					methodResult = methodInfo.Invoke(classInstance, invokeParams);
				}
				var result = (methodResult is string s)
					? s
					: Client.Serialize(methodResult);
				return result;
			}
			return null;
		}

		public static async Task<bool> ApproveExecution(TemplateItem item, PluginApprovalItem pfci, CancellationTokenSource cancellationTokenSource)
		{
			if (item.PluginApprovalProcess == ToolCallApprovalProcess.DenyAll)
				return false;
			if (item.PluginApprovalProcess == ToolCallApprovalProcess.AllowAll)
				return true;
			var assistantApproved = false;
			string assistantEvaluation = null;
			if (item.PluginApprovalProcess == ToolCallApprovalProcess.UserWhenAssitantDenies || item.PluginApprovalProcess == ToolCallApprovalProcess.Assistant)
			{
				assistantEvaluation = await ClientHelper.EvaluateToolExecutionSafety(item, cancellationTokenSource) ?? "";
				Global.MainControl.Dispatcher.Invoke(() =>
				{
					var lastMessage = item.Messages.Last();
					var attachment = new Controls.Chat.MessageAttachments(ContextType.None, "text", assistantEvaluation);
					attachment.Title = "Approval by Secondary AI";
					attachment.IsAlwaysIncluded = true;
					lastMessage.Attachments.Add(attachment);
				});
				assistantApproved = assistantEvaluation.ToLower().Contains("function call approved");
				// If approval relies on AI assistan only then return result.
				if (item.PluginApprovalProcess == ToolCallApprovalProcess.Assistant)
					return assistantApproved;
				pfci.SecondaryAiEvaluation = JocysCom.ClassLibrary.Text.Helper.CropLines(assistantEvaluation ?? "", 32);
			}
			if (item.PluginApprovalProcess == ToolCallApprovalProcess.User || item.PluginApprovalProcess == ToolCallApprovalProcess.UserWhenAssitantDenies)
			{
				// If assitant approved then return true.
				if (item.PluginApprovalProcess == ToolCallApprovalProcess.UserWhenAssitantDenies && assistantApproved)
					return true;

				// This will make approval form on template panel visible.
				Global.MainControl.Dispatcher.Invoke(() =>
				{
					item.PluginFunctionCalls.Add(pfci);
				});
				// Wait for approval (semaphore release)
				try
				{
					pfci.Semaphore.Wait(cancellationTokenSource.Token);
				}
				catch (Exception)
				{
				}
				Global.MainControl.Dispatcher.Invoke(() =>
				{
					item.PluginFunctionCalls.Remove(pfci);
				});
				if (pfci.IsApproved != null)
					return pfci.IsApproved.Value;
			}
			return false;
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
			var ToolDefinitions = new List<ChatCompletionsFunctionToolDefinition>();
			foreach (var kv in PluginFunctions)
			{
				if (!AllowPlugin(kv.Key, item.MaxRiskLevel))
					continue;
				var requiredParams = new List<string>();
				// Get Method Info
				var mi = kv.Value;
				var summaryText = XmlDocHelper.GetSummaryText(mi).Trim(new char[] { '\r', '\n', ' ' });
				var miParams = mi.GetParameters();
				var parametersObject = new Dictionary<string, object>();
				foreach (var pi in miParams)
				{
					var paramText = XmlDocHelper.GetParamText(mi, pi).Trim(new char[] { '\r', '\n', ' ' });
					if (!pi.IsOptional)
						requiredParams.Add(pi.Name);
					var underlyingType = Nullable.GetUnderlyingType(pi.ParameterType) ?? pi.ParameterType;
					var typeInfo = GetJsonType(pi.ParameterType);
					if (underlyingType.IsArray)
					{
						var elementType = underlyingType.GetElementType();
						parametersObject.Add(pi.Name, new
						{
							type = "array",
							items = new { type = GetJsonType(elementType) },
							description = paramText
						});
					}
					else
					{
						parametersObject.Add(pi.Name, new
						{
							type = typeInfo,
							description = paramText
						});
					}
				}
				// Serialize the parameters object to a JSON string then create a BinaryData instance.
				var serializedParameters = JsonSerializer.Serialize(new
				{
					type = "object",
					properties = parametersObject,
					required = requiredParams.ToArray(),
				});
				var binaryParamaters = BinaryData.FromString(serializedParameters);
				// Create and add function definition.
				var function = new FunctionDefinition();
				function.Name = mi.Name;
				function.Description = summaryText;
				function.Parameters = binaryParamaters;
				var tool = new ChatCompletionsFunctionToolDefinition(function);
				ToolDefinitions.Add(tool);
			}
			if (ToolDefinitions.Any())
			{
				foreach (var tool in ToolDefinitions)
					options.Tools.Add(tool);
				options.ToolChoice = ChatCompletionsToolChoice.Auto;
			}
		}

		public static object GetJsonType(Type type)
		{
			// Determine the underlying non-nullable type of the parameter
			var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

			// Enum handling: Return a JSON structure describing the enum, including possible values
			if (underlyingType.IsEnum)
			{
				//var enumNames = Enum.GetNames(underlyingType);
				//return new
				//{
				//	type = "string",
				//	@enum = enumNames
				//};
				return "string";
			}
			// Simple types: Return the JSON type name as a string
			switch (Type.GetTypeCode(underlyingType))
			{
				case TypeCode.Boolean: return "boolean";
				case TypeCode.Byte:
				case TypeCode.Int16:
				case TypeCode.Int32:
				case TypeCode.Int64:
				case TypeCode.SByte:
				case TypeCode.UInt16:
				case TypeCode.UInt32:
				case TypeCode.UInt64: return "integer";
				case TypeCode.Decimal:
				case TypeCode.Double:
				case TypeCode.Single: return "number";
				case TypeCode.String: return "string";
				default:
					// For all other types, including complex objects and types not explicitly handled above
					return "object";
			}
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
			var CompletionTools = new List<chat_completion_tool>();
			foreach (var kv in PluginFunctions)
			{
				if (!AllowPlugin(kv.Key, item.MaxRiskLevel))
					continue;
				var mi = kv.Value;
				var summaryText = XmlDocHelper.GetSummaryText(mi).Trim(new char[] { '\r', '\n', ' ' });
				var requiredParams = new List<string>();
				var props = new Dictionary<string, object>();
				foreach (var pi in mi.GetParameters())
				{
					var paramText = XmlDocHelper.GetParamText(mi, pi).Trim(new char[] { '\r', '\n', ' ' });
					if (!pi.IsOptional)
						requiredParams.Add(pi.Name);
					props[pi.Name] = new
					{
						type = GetJsonType(pi.ParameterType),
						description = paramText
					};
				}
				var function = new chat_completion_function()
				{
					name = mi.Name,
					description = summaryText,
					parameters = new base_item
					{
						additional_properties = new Dictionary<string, JsonElement>
						{
							["parameters"] = JsonDocument.Parse(JsonSerializer.Serialize(new
							{
								type = "object",
								properties = props,
								required = requiredParams
							})).RootElement
						}
					}
				};
				var tool = new chat_completion_tool()
				{
					type = chat_completion_tool_type.function,
					function = function,
				};
				CompletionTools.Add(tool);
			}
			if (CompletionTools.Any())
			{
				// No need to lock here as we are only reading from CompletionTools
				foreach (var tool in CompletionTools)
					request.tools.Add(tool);
				request.tool_choice = tool_choice.auto;
			}
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
				}
			}
		}

		#endregion

	}
}
