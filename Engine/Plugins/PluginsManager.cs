using DocumentFormat.OpenXml;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Xml;
using JocysCom.VS.AiCompanion.Engine.Companions;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using JocysCom.VS.AiCompanion.Plugins.Core;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using OpenAI.Chat;
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
	public partial class PluginsManager
	{

		#region Manage Functions

		/// <summary>
		/// Store method names with method info.
		/// </summary>
		public static List<PluginItem> PluginFunctions
		{
			get
			{
				lock (PluginFunctionsLock)
				{
					if (_PluginFunctions == null)
						_PluginFunctions = new List<PluginItem>();
					if (_PluginFunctions.Count == 0)
					{
						AddMethods(typeof(Basic));
						AddMethods(typeof(Web));
						AddMethods(typeof(Mail));
						AddMethods(typeof(VisualStudio));
						AddMethods(typeof(Database));
						Search._databasePath = Global.PluginsSearchPath;
						AddMethods(typeof(Search));
						AddMethods(typeof(Multimedia));
						AddMethods(typeof(Lists));
#if DEBUG
						AddMethods(typeof(Automation));
						API_LoadPlugins(Global.PluginsPath);
#endif
					}
					return _PluginFunctions;
				}
			}
		}
		static List<PluginItem> _PluginFunctions;
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
				var attribute = mi.GetCustomAttributes().FirstOrDefault(x => x.GetType().Name == nameof(RiskLevelAttribute));
				if (attribute is null)
					continue;
				// Get level from attribute using reflection to access Level property
				var levelProperty = attribute.GetType().GetProperty(nameof(RiskLevelAttribute.Level));
				if (levelProperty != null)
				{
					var levelValue = (RiskLevel)levelProperty.GetValue(attribute);
					if (levelValue > RiskLevel.Unknown)
						_PluginFunctions.Add(new PluginItem(mi));
				}
			}
		}

		private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
		{
			// Strip version and public key token as needed to simplify matches
			string assemblyFullName = args.Name.Split(',')[0] + ".dll";

			// Attempt to load the assembly from globally known paths or cache directories
			string[] possiblePaths = new string[]
			{
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyFullName),
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
				 @"dotnet\shared\Microsoft.AspNetCore.App\" + assemblyFullName)
				// Add more standard paths if known
			};

			foreach (var path in possiblePaths)
			{
				if (File.Exists(path))
				{
					return Assembly.LoadFrom(path);
				}
			}

			// Let the runtime attempt default methods if specific paths fail
			return null;
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
		/// Call function requested by AI Model.
		/// </summary>
		/// <param name="item">User settings.</param>
		/// <param name="function">function as JSON</param>
		public static async Task<(string, string)?> ProcessPluginFunction(TemplateItem item, chat_completion_function function, CancellationTokenSource cancellationTokenSource)
		{
			if (!item.PluginsEnabled)
				return (null, null);
			var maxRiskLevel = (RiskLevel)Math.Min((int)item.MaxRiskLevel, (int)AppHelper.GetMaxRiskLevel());
			if (!AllowPluginFunction(function.name, maxRiskLevel))
				return null;
			var methodInfo = PluginFunctions.FirstOrDefault(x => x.Name == function.name)?.Mi;
			if (methodInfo is null)
			{
				// Handle the case where the methodInfo is not found for the given functionName
				MessageBox.Show($"The function '{function.name}' was not found.", "Execution Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return null;
			}
			if (item.PluginApprovalProcess == ToolCallApprovalProcess.DenyAll)
				return ("text", Resources.MainResources.main_Call_function_request_denied);
			object classInstance = null;
			// If the method is not static, create an instance of the class.
			if (!methodInfo.IsStatic)
				classInstance = Activator.CreateInstance(methodInfo.DeclaringType);
			// Prepare an array of parameters for the method invocation.
			var invokeParams = ConvertFromToolItem(methodInfo, function);
			var pfci = new PluginApprovalItem();
			PluginItem plugin = null;
			ControlsHelper.Invoke(() =>
			{
				plugin = new PluginItem(methodInfo);
			});

			if (plugin.RiskLevel != RiskLevel.None)
			{
				pfci.Plugin = plugin;
				pfci.function = function;
				// Select values submitted for param.
				var parameters = function.parameters.additional_properties;
				var contextParamName = FunctionInvocationContext.ContextParameterInfos[0].Name;
				if (parameters.Keys.Contains(contextParamName))
				{
					var contextValue = parameters[contextParamName];
					pfci.ReasonForInvocation = contextValue.ToString() ?? "";
				}
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
				{
					var messageToAI = ClientHelper.JoinMessageParts(
						string.IsNullOrEmpty(pfci.ApprovalReason)
							? null
							: $"Request Denial Comments: {pfci.ApprovalReason}"
						,
						Resources.MainResources.main_Call_function_request_denied);
					return ("text", messageToAI);
				}
			}
			object methodResult = null;
			if (methodInfo.DeclaringType.Name == nameof(VisualStudio))
			{
				await Global.MainControl.Dispatcher.Invoke(async () =>
				{
					await Global.SwitchToVisualStudioThreadAsync(cancellationTokenSource.Token);
					methodResult = await InvokeMethod(methodInfo, classInstance, invokeParams, true, cancellationTokenSource.Token);
				});
			}
			else if (classInstance is Search search)
			{
				await Global.MainControl.Dispatcher.Invoke(async () =>
				{
					var eh = new EmbeddingHelper();
					eh.Item = item;
					search.SearchEmbeddingsCallback = eh.SearchEmbeddingsToSystemMessage;
					methodResult = await InvokeMethod(methodInfo, search, invokeParams, true, cancellationTokenSource.Token);
					search.SearchEmbeddingsCallback = null;
				});
			}
			else if (classInstance is Multimedia mm)
			{
				await Global.MainControl.Dispatcher.Invoke(async () =>
				{
					var ai = new AiMultimediaClient();
					ai.Item = item;
					// Map Text, Audio and Video converter methods.
					mm.VideoToText = ai.VideoToText;
					mm.GetStructuredImageAnalysisInstructions = () => Global.AppSettings.StructuredImageAnalysisInstructions;
					mm.AISpeakCallback = Global.AvatarOptionsPanel.AI_SpeakSSML;
					//mm.CaptureCameraImageCallback = CameraHelper.CaptureCameraImage;
					methodResult = await InvokeMethod(methodInfo, mm, invokeParams, true, cancellationTokenSource.Token);
					mm.CaptureCameraImageCallback = null;
					mm.VideoToText = null;
					mm.AISpeakCallback = null;
					ai.Item = null;
				});
			}
			else if (classInstance is Mail mail)
			{
				await Global.MainControl.Dispatcher.Invoke(async () =>
				{
					item.UpdateMailClientAccount();
					mail.SendCallback = item.AiMailClient.Send;
					methodResult = await InvokeMethod(methodInfo, mail, invokeParams, true, cancellationTokenSource.Token);
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
					methodResult = await InvokeMethod(methodInfo, lists, invokeParams, true, cancellationTokenSource.Token);
					// Fix lists with no icons.
					var noIconLists = Global.Lists.Items.Where(x => x.IconData == null).ToList();
					foreach (var noIconList in noIconLists)
						AppHelper.SetIconToDefault(noIconList);
				});
			}
			else
			{
				methodResult = await InvokeMethod(methodInfo, classInstance, invokeParams, false, cancellationTokenSource.Token);
			}
			var result = (methodResult is string s)
				? ("text", s)
				: ("text", Client.Serialize(methodResult));
			return result;
		}

		public interface ICancelableTask
		{
			void Cancel();
		}


		public static async Task<object> InvokeMethod(
			System.Reflection.MethodInfo methodInfo, object classInstance, object[] invokeParams,
			bool invokeOnUIThread = false,
			CancellationToken cancellationToken = default)
		{
			// Check if the method is asynchronous (either returning Task or Task<T>)
			bool isAsyncMethod = typeof(Task).IsAssignableFrom(methodInfo.ReturnType);
			// Check if the method is void or Task (for async method)
			bool isVoidMethod = methodInfo.ReturnType == typeof(void) || methodInfo.ReturnType == typeof(Task);
			if (isAsyncMethod)
			{
				var supportsCancellation = methodInfo.GetParameters()
					 .Any(param => param.ParameterType == typeof(CancellationToken));
				// Invoke the method
				var task = (Task)methodInfo.Invoke(classInstance, invokeParams);
				// Register a callback on the cancellation token to cancel the task if requested
				cancellationToken.Register(() =>
				{
					if (task is ICancelableTask cancelableTask)
						cancelableTask.Cancel();
				});
				if (supportsCancellation)
				{
					// Ensure you await the task
					await task.ConfigureAwait(false);
				}
				else
				{
					// Handle the task if it doesn't support cancellation
					var completedTask = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cancellationToken));
					if (completedTask == task)
						await task.ConfigureAwait(false);
					else
						throw new OperationCanceledException(cancellationToken);
				}
				// Handle async methods that return a value (Task<T>)
				var isTaskReturnType = methodInfo.ReturnType.IsGenericType &&
					typeof(Task).IsAssignableFrom(methodInfo.ReturnType.GetGenericTypeDefinition());
				if (isTaskReturnType)
				{
					if (cancellationToken.IsCancellationRequested)
						throw new OperationCanceledException(cancellationToken);
					// Extract the result from Task<T>
					var resultProperty = task.GetType().GetProperty(nameof(Task<object>.Result));
					return resultProperty.GetValue(task);
				}
			}
			else
			{
				// For synchronous methods, directly invoke and return the result (or null if void)
				// Invoke the method and handle potential cancellation
				return await Task.Run(() =>
				{
					if (cancellationToken.IsCancellationRequested)
						throw new OperationCanceledException(cancellationToken);
					if (!invokeOnUIThread)
						return methodInfo.Invoke(classInstance, invokeParams);
					object result = null;
					Global.MainControl.Dispatcher.Invoke(() =>
					{
						result = methodInfo.Invoke(classInstance, invokeParams);
					});
					return result;

				}, cancellationToken);
			}
			// Return null if it's a void method (synchronous or asynchronous)
			if (cancellationToken.IsCancellationRequested)
				throw new OperationCanceledException(cancellationToken);
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
				// Evaluate the request to execute a function by another AI.
				assistantEvaluation = await ClientHelper.EvaluateToolExecutionSafety(item, cancellationTokenSource) ?? "";
				ControlsHelper.AppInvoke(() =>
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
				ControlsHelper.AppInvoke(() =>
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
				ControlsHelper.AppInvoke(() =>
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
		public static void ProvideTools(TemplateItem item, ChatCompletionOptions options)
		{
			if (!item.PluginsEnabled)
				return;
			var ToolDefinitions = new List<ChatTool>();
			foreach (var pluginItem in PluginFunctions)
			{
				var maxRiskLevel = (RiskLevel)Math.Min((int)item.MaxRiskLevel, (int)AppHelper.GetMaxRiskLevel());
				if (!AllowPluginFunction(pluginItem.Name, maxRiskLevel))
					continue;
				// Get Method Info
				var mi = pluginItem.Mi;
				// If this is not a Visual Studio extension but a plugin for Visual Studio, then skip.
				if (!Global.IsVsExtension && mi.DeclaringType.Name == nameof(VisualStudio))
					continue;
				// Serialize the parameters object to a JSON string then create a BinaryData instance.
				var functionParameters = ConvertToToolItem(null, mi);
				// Add extra parameter that will make AI to supply rationale when invoking a function.
				var contextMi = FunctionInvocationContext.ContextMethodInfo;
				var contextPis = FunctionInvocationContext.ContextParameterInfos;
				foreach (var contextPi in contextPis)
				{
					var list = functionParameters.required.ToList();
					list.Insert(0, contextPi.Name);
					functionParameters.required = list.ToArray();
					var contextItem = ConvertToToolItem(null, contextMi, contextPi);
					functionParameters.properties.Add(contextPi.Name, contextItem);
				}
				// Continue.
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
				var tool = ChatTool.CreateFunctionTool(
					mi.Name,
					string.Join("\r\n\r\n", lines),
					binaryParamaters
				);
				ToolDefinitions.Add(tool);
			}
			if (ToolDefinitions.Any())
			{
				// Need to use reflection to set the Temperature property
				// because the developers used unnecessary C# 9.0 features that won't work on .NET 4.8.
				var value = ChatToolChoice.Auto;
				// Make sure that last message is not automated reply or it will go into the infinite loop.
				if (item.ToolChoiceRequired && !(item.Messages.Last()?.IsAutomated == true))
				{
					value = ChatToolChoice.Required;
					var requiredFunctions = ToolDefinitions.Where(x => item.ToolChoiceRequiredNames.Contains(x.FunctionName)).ToList();
					foreach (var tool in requiredFunctions)
						options.Tools.Add(tool);
				}
				// If no required tools are added, then...
				if (!options.Tools.Any())
				{
					// Add all functions for execution.
					foreach (var tool in ToolDefinitions)
						options.Tools.Add(tool);
				}
				typeof(ChatCompletionOptions)
					.GetProperty(nameof(ChatCompletionOptions.ToolChoice), BindingFlags.Public | BindingFlags.Instance)
						?.SetValue(options, value, null);
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
					item.description = XmlDocHelper.GetSummaryText(property, FormatText.RemoveIdentAndTrimSpaces);
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
			if (methodInfo is null)
				return null;
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

		public static List<chat_completion_tool> GetCompletionTools(RiskLevel maxRiskLevel)
		{
			var completionTools = new List<chat_completion_tool>();
			foreach (var pluginItem in PluginFunctions)
			{
				if (!AllowPluginFunction(pluginItem.Name, maxRiskLevel))
					continue;
				var mi = pluginItem.Mi;
				var summaryText = XmlDocHelper.GetSummaryText(mi, FormatText.RemoveIdentAndTrimSpaces);
				var requiredParams = new List<string>();
				var props = new Dictionary<string, object>();
				// Add extra parameter that will make AI to supply rationale when invoking a function.
				var contextMi = FunctionInvocationContext.ContextMethodInfo;
				var contextPis = FunctionInvocationContext.ContextParameterInfos;
				foreach (var contextPi in contextPis)
				{
					requiredParams.Add(contextPi.Name);
					props[contextPi.Name] = ConvertToToolItem(null, contextMi, contextPi);
				}
				// Add actual method properties.
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
							["parameters"] = JsonDocument.Parse(Client.Serialize(new
							chat_completion_function_parameter
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
				completionTools.Add(tool);
			}
			return completionTools;
		}

		/// <summary>
		/// Add completion tools to chat completion message request for OpenAI GPT.
		/// </summary>
		/// <param name="item">Template item with settings.</param>
		/// <param name="request">Chat completion request</param>
		public static void ProvideTools(TemplateItem item, chat_completion_request request)
		{
			if (!item.PluginsEnabled)
				return;
			var maxRiskLevel = (RiskLevel)Math.Min((int)item.MaxRiskLevel, (int)AppHelper.GetMaxRiskLevel());
			var completionTools = GetCompletionTools(maxRiskLevel);
			if (completionTools.Any())
			{
				var value = tool_choice.auto;
				// Make sure that last message is not automated reply or it will go into the infinite loop.
				if (item.ToolChoiceRequired && !(item.Messages.Last()?.IsAutomated == true))
				{
					value = tool_choice.required;
					var requiredFunctions = completionTools.Where(x => item.ToolChoiceRequiredNames.Contains(x.function.name)).ToList();
					foreach (var tool in requiredFunctions)
						request.tools.Add(tool);
				}
				// If no required tools are added, then...
				if (!request.tools.Any())
				{
					// Add all functions for execution.
					foreach (var tool in completionTools)
						request.tools.Add(tool);
				}
				request.tool_choice = value;
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
