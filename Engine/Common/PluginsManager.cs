using Azure.AI.OpenAI;
using DocumentFormat.OpenXml;
using JocysCom.ClassLibrary.Runtime;
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
						AddMethods(typeof(Web));
						AddMethods(typeof(Mail));
						AddMethods(typeof(VisualStudio));
						AddMethods(typeof(Database));
						Search._databasePath = Global.PluginsSearchPath;
						AddMethods(typeof(Search));
						AddMethods(typeof(TTS));
						AddMethods(typeof(Lists));
#if DEBUG
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
			foreach (var mi in methods)
			{
				var rla = Attributes.FindCustomAttribute<RiskLevelAttribute>(mi);
				if (rla == null || rla.Level <= RiskLevel.Unknown)
					continue;
				_PluginFunctions.Add(mi.Name, mi);
			}
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
					AddMethods(type);
				}
			}
		}

		public static bool AllowPluginFunction(string functionName, RiskLevel maxRiskLevel)
		{
			var currentPlugin = Global.AppSettings.Plugins.FirstOrDefault(x => x.Name == functionName);
			// Deny disabled plugins.
			if (currentPlugin?.IsEnabled != true)
				return false;
			// Deny if risk unknown.
			if (currentPlugin.RiskLevel < RiskLevel.None)
				return false;
			// Deny if risk is higher than selected by the task.	
			if (currentPlugin.RiskLevel > maxRiskLevel)
				return false;
			// Deny if this is not a Visual Studio extension but a plugin for Visual Studio.
			if (!Global.IsVsExtension && currentPlugin.Mi.DeclaringType.Name == nameof(VisualStudio))
				return false;
			return true;
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
			var maxRiskLevel = (RiskLevel)Math.Min((int)item.MaxRiskLevel, (int)(DomainHelper.GetUserMaxRiskLevel() ?? RiskLevel.Critical));
			if (!AllowPluginFunction(function.name, maxRiskLevel))
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
			var invokeParams = ConvertFromToolItem(methodInfo, function);
			var pfci = new PluginApprovalItem();
			PluginItem plugin = null;
			Global.MainControl.Dispatcher.Invoke(() =>
			{
				plugin = new PluginItem(methodInfo);
			});

			if (plugin.RiskLevel != RiskLevel.None)
			{
				pfci.Plugin = plugin;
				pfci.function = function;
				// Select values submitted for param.
				var parameters = function.parameters.additional_properties;
				for (int p = 0; p < plugin.Params.Count; p++)
				{
					var param = plugin.Params[p];
					param.ParamValuePreview = JocysCom.ClassLibrary.Text.Helper.CropLines(invokeParams[p]?.ToString() ?? "");
					// Hide non supplied optional parameters.
					if (parameters == null)
						parameters = new Dictionary<string, JsonElement>();
					if (param.IsOptional && !parameters.Keys.Contains(param.Name))
						param.ParamVisibility = Visibility.Collapsed;
				}
				var approved = await ApproveExecution(item, pfci, cancellationTokenSource);
				if (!approved)
					return Resources.Resources.Call_function_request_denied;
			}
			object methodResult = null;
			if (methodInfo.DeclaringType.Name == nameof(VisualStudio))
			{
				await Global.MainControl.Dispatcher.Invoke(async () =>
				{
					await Global.SwitchToVisualStudioThreadAsync();
					methodResult = await InvokeMethod(methodInfo, classInstance, invokeParams);
				});
			}
			else if (classInstance is Search search)
			{
				await Global.MainControl.Dispatcher.Invoke(async () =>
				{
					var eh = new EmbeddingHelper();
					eh.Item = item;
					search.SearchEmbeddingsCallback = eh.SearchEmbeddingsToSystemMessage;
					methodResult = await InvokeMethod(methodInfo, search, invokeParams);
					search.SearchEmbeddingsCallback = null;
				});
			}
			else if (classInstance is Mail mail)
			{
				await Global.MainControl.Dispatcher.Invoke(async () =>
				{
					item.UpdateMailClientAccount();
					mail.SendCallback = item.AiMailClient.Send;
					methodResult = await InvokeMethod(methodInfo, mail, invokeParams);
					mail.SendCallback = null;
				});
			}
			else if (classInstance is Lists lists)
			{
				// Make sure that the list have the name of the task.
				// If task is renamed then relevant lists must be renamed too.
				lists.FilterPath = item.Name;
				await Global.MainControl.Dispatcher.Invoke(async () =>
				{
					methodResult = await InvokeMethod(methodInfo, lists, invokeParams);
					// Fix lists with no icons.
					var noIconLists = Global.Lists.Items.Where(x => x.IconData == null).ToList();
					foreach (var noIconList in noIconLists)
						AppHelper.SetIconToDefault(noIconList);
				});
			}
			else
			{
				methodResult = await InvokeMethod(methodInfo, classInstance, invokeParams);
			}
			var result = (methodResult is string s)
				? s
				: Client.Serialize(methodResult);
			return result;
		}

		public static async Task<object> InvokeMethod(System.Reflection.MethodInfo methodInfo, object classInstance, object[] invokeParams)
		{
			// Check if the method is asynchronous (either returning Task or Task<T>)
			bool isAsyncMethod = typeof(Task).IsAssignableFrom(methodInfo.ReturnType);
			// Check if the method is void or Task (for async method)
			bool isVoidMethod = methodInfo.ReturnType == typeof(void) || methodInfo.ReturnType == typeof(Task);
			if (isAsyncMethod)
			{
				var task = (Task)methodInfo.Invoke(classInstance, invokeParams);
				await task.ConfigureAwait(false); // Ensure you await the task
												  // Handle async methods that return a value (Task<T>)
				if (!isVoidMethod) // It means it's Task<T>
				{
					// Extract the result from Task<T>
					var resultProperty = task.GetType().GetProperty("Result");
					return resultProperty.GetValue(task);
				}
			}
			else
			{
				// For synchronous methods, directly invoke and return the result (or null if void)
				if (!isVoidMethod) // Has return value
				{
					return methodInfo.Invoke(classInstance, invokeParams);
				}
			}
			// Return null if it's a void method (synchronous or asynchronous)
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
				var maxRiskLevel = (RiskLevel)Math.Min((int)item.MaxRiskLevel, (int)(DomainHelper.GetUserMaxRiskLevel() ?? RiskLevel.Critical));
				if (!AllowPluginFunction(kv.Key, maxRiskLevel))
					continue;
				// Get Method Info
				var mi = kv.Value;
				// If this is not a Visual Studio extension but a plugin for Visual Studio, then skip.
				if (!Global.IsVsExtension && mi.DeclaringType.Name == nameof(VisualStudio))
					continue;
				// Serialize the parameters object to a JSON string then create a BinaryData instance.
				var functionParameters = ConvertToToolItem(null, mi);
				var serializedParameters = Client.Serialize(functionParameters);
				var binaryParamaters = BinaryData.FromString(serializedParameters);
				var summary = XmlDocHelper.GetSummaryText(mi, FormatText.RemoveIdentAndTrimSpaces);
				var returns = XmlDocHelper.GetReturnText(mi, FormatText.RemoveIdentAndTrimSpaces);
				//var example = XmlDocHelper.GetExampleText(mi, FormatText.RemoveIdentAndTrimSpaces);
				var lines = new List<string>();
				if (!string.IsNullOrEmpty(summary))
					lines.Add(summary);
				if (!string.IsNullOrEmpty(returns))
					lines.Add("Returns:\r\n" + returns);
				//if (!string.IsNullOrEmpty(example))
				//	lines.Add("Example:\r\n" + example);
				// Create and add function definition.
				var function = new FunctionDefinition();
				function.Name = mi.Name;
				function.Description = string.Join("\r\n\r\n", lines);
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

		#endregion

		#region Function Converter

		public static tool_item ConvertToToolItem(
			Type type,
			System.Reflection.MethodInfo mi = null, ParameterInfo miPi = null
		)
		{
			var o = new tool_item();
			string oDescription;
			Type oType;
			// Method
			if (mi != null && miPi == null)
			{
				var args = mi.GetParameters();
				o.type = GetJsonMainType(mi);
				o.required = args.Where(x => !x.IsOptional).Select(x => x.Name).ToArray();
				var oProperties = new Dictionary<string, tool_item>();
				foreach (var arg in args)
				{
					var item = ConvertToToolItem(arg.ParameterType);
					item.description = XmlDocHelper.GetParamText(mi, arg, FormatText.RemoveIdentAndTrimSpaces);
					oProperties.Add(arg.Name, item);
				}
				o.properties = oProperties;
				return o;
			}
			// Method parameter
			else if (mi != null && miPi != null)
			{
				oType = miPi.ParameterType;
				oDescription = XmlDocHelper.GetParamText(mi, miPi, FormatText.RemoveIdentAndTrimSpaces);
			}
			// Property type.
			else
			{
				oType = type;
				oDescription = XmlDocHelper.GetSummary(type, FormatText.RemoveIdentAndTrimSpaces);
			}
			var underlyingType = Nullable.GetUnderlyingType(oType) ?? oType;
			o.type = GetJsonMainType(oType);
			o.description = oDescription;
			if (underlyingType == typeof(DateTime))
			{
				o.format = "date-time";
				return o;
			}
			if (underlyingType.IsArray)
			{
				var elementType = underlyingType.GetElementType();
				o.items = ConvertToToolItem(type: elementType);
				return o;
			}
			if (underlyingType.IsEnum)
			{
				o.@enum = Enum.GetNames(underlyingType);
				return o;
			}
			if (o.type == "object")
			{
				var properties = oType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
				var oProperties = new Dictionary<string, tool_item>();
				foreach (var property in properties)
				{
					var item = ConvertToToolItem(property.PropertyType);
					oProperties.Add(property.Name, item);
				}
				o.properties = oProperties;
			}
			return o;
		}

		public static string GetJsonMainType(object o)
		{
			if (o is Type type)
			{
				var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
				if (underlyingType.IsArray)
					return "array";
				if (underlyingType.IsEnum)
					return "string";
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
				}
			}
			return "object";
		}

		/// <summary>
		/// Convert incommming JSON function parameter to object array to pass into C# function.
		/// </summary>
		/// <param name="methodInfo">C# Method info.</param>
		/// <param name="function">Incomming parameters.</param>
		/// <returns>C# function arguments.</returns>
		public static object[] ConvertFromToolItem(System.Reflection.MethodInfo methodInfo, chat_completion_function function)
		{
			var methodParams = methodInfo.GetParameters();
			var invokeParams = new object[methodParams.Length];
			for (int i = 0; i < methodParams.Length; i++)
			{
				var param = methodParams[i];
				var type = param.ParameterType;
				var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
				JsonElement jsonElement;
				// Extract parameters as a dictionary.
				var parameters = function.parameters.additional_properties;
				if (parameters == null)
					parameters = new Dictionary<string, JsonElement>();
				if (parameters.TryGetValue(param.Name, out jsonElement))
				{
					if (underlyingType == typeof(DateTime))
					{
						var stringValue = jsonElement.GetString();
						var dateValue = DateTime.Parse(stringValue);
						invokeParams[i] = dateValue;
					}
					if (underlyingType.IsEnum)
					{
						// Assuming the JSON element is a string that matches the enum name.
						var stringValue = jsonElement.GetString();
						var enumValue = Enum.Parse(underlyingType, stringValue);
						invokeParams[i] = enumValue;
					}
					else if (underlyingType.IsArray)
					{
						var elementType = underlyingType.GetElementType();
						if (jsonElement.ValueKind == JsonValueKind.Array)
						{
							var elements = new List<object>();
							foreach (var arrayItem in jsonElement.EnumerateArray())
							{
								// Deserialize each element to the specified element type.
								var element = arrayItem.Deserialize(elementType);
								elements.Add(element);
							}
							// Create an array of the appropriate type and assign the elements.
							var array = Array.CreateInstance(elementType, elements.Count);
							for (var j = 0; j < elements.Count; j++)
								array.SetValue(elements[j], j);
							invokeParams[i] = array;
						}
						else
						{
							// Handle error: expected JSON array for parameter type.
							MessageBox.Show($"Expected a JSON array for parameter '{param.Name}' but got a different type.", "Execution Error", MessageBoxButton.OK, MessageBoxImage.Error);
							return null;
						}
					}
					else
					{
						invokeParams[i] = jsonElement.Deserialize(param.ParameterType);
					}
				}
				else if (param.HasDefaultValue)
				{
					invokeParams[i] = param.DefaultValue;
				}
				else
				{
					// Handle missing required parameter.
					MessageBox.Show($"The required parameter '{param.Name}' is missing for the function '{function.name}'.", "Execution Error", MessageBoxButton.OK, MessageBoxImage.Error);
					return invokeParams;
				}
			}
			return invokeParams;
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
				var maxRiskLevel = (RiskLevel)Math.Min((int)item.MaxRiskLevel, (int)(DomainHelper.GetUserMaxRiskLevel() ?? RiskLevel.Critical));
				if (!AllowPluginFunction(kv.Key, maxRiskLevel))
					continue;
				var mi = kv.Value;
				var summaryText = XmlDocHelper.GetSummaryText(mi, FormatText.RemoveIdentAndTrimSpaces);
				var requiredParams = new List<string>();
				var props = new Dictionary<string, object>();
				foreach (var pi in mi.GetParameters())
				{
					if (!pi.IsOptional)
						requiredParams.Add(pi.Name);
					props[pi.Name] = ConvertToToolItem(null, mi, pi);
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
