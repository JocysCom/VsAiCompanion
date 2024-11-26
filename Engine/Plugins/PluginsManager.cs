using DocumentFormat.OpenXml;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Xml;
using JocysCom.VS.AiCompanion.Engine.Companions;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using JocysCom.VS.AiCompanion.Engine.Controls.Chat;
using JocysCom.VS.AiCompanion.Plugins.Core;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using Namotion.Reflection;
using NJsonSchema.Generation;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
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

		private static readonly SemaphoreSlim _pluginFunctionsSemaphore = new SemaphoreSlim(1, 1);

		/// <summary>
		/// Store method names with method info.
		/// </summary>
		public static List<PluginItem> GetPluginFunctions()
		{
			_pluginFunctionsSemaphore.Wait();
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
				AddMethods(typeof(Automation));
#if DEBUG
				AddMethods(typeof(Workflow));
#endif
				JocysCom.ClassLibrary.Helper.RunSynchronously(async () =>
					await API_LoadPlugins(Global.PluginsPath));
			}
			_pluginFunctionsSemaphore.Release();
			return _PluginFunctions;
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
				var attribute = ClassLibrary.Runtime.Attributes.FindCustomAttribute<RiskLevelAttribute>(mi);
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
			if (!Global.IsVsExtension && currentPlugin.Class == nameof(VisualStudio))
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
			var methodInfo = GetPluginFunctions().FirstOrDefault(x => x.Name == function.name)?.Mi;
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
			plugin.InvokeParams = invokeParams;
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
			else if (classInstance is Database database)
			{
				await Global.MainControl.Dispatcher.Invoke(async () =>
				{
					var path = Global.GetPath(item);
					// Map Text, Audio and Video converter methods.
					database.GetDatabasesFolderPath = () => { return path; };
					methodResult = await InvokeMethod(methodInfo, database, invokeParams, true, cancellationTokenSource.Token);
					database.GetDatabasesFolderPath = null;
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
					mm.GetTempFolderPath = AppHelper.GetTempFolderPath;
					mm.GenerateImageCallback = ai.CreateImageAsync;
					mm.ModifyImageCallback = ai.ModifyImageAsync;
					mm.GetStructuredImageAnalysisInstructions = () => Global.AppSettings.StructuredImageAnalysisInstructions;
					mm.AISpeakCallback = Global.AvatarOptionsPanel.AI_SpeakSSML;
					//mm.CaptureCameraImageCallback = CameraHelper.CaptureCameraImage;
					methodResult = await InvokeMethod(methodInfo, mm, invokeParams, true, cancellationTokenSource.Token);
					mm.CaptureCameraImageCallback = null;
					mm.VideoToText = null;
					mm.ModifyImageCallback = null;
					mm.GenerateImageCallback = null;
					mm.GetTempFolderPath = null;
					mm.AISpeakCallback = null;
					ai.Item = null;
				});
			}
			else if (classInstance is Workflow wf)
			{
				await Global.MainControl.Dispatcher.Invoke(async () =>
				{
					wf.ExecutePlanCallback = async (plan, cancellationToken) =>
					{
						var we = new Plugins.Core.Workflows.WorkflowExecutor();
						var methods = GetPluginFunctions().Where(x => x.Mi != null).Select(x => x.Mi).ToArray();
						await we.ExecutePlan(plan, methods, cancellationToken);
						return new ClassLibrary.OperationResult<bool>(true);
					};
					methodResult = await InvokeMethod(methodInfo, wf, invokeParams, true, cancellationTokenSource.Token);
					wf.ExecutePlanCallback = null;
				});
			}

			else if (classInstance is Automation am)
			{
				await Global.MainControl.Dispatcher.Invoke(async () =>
				{
					var ac = new AutomationClient();
					ac.Item = item;
					am.GetCanvasEditorElementPath = ac.GetCanvasEditorElementPath;
					methodResult = await InvokeMethod(methodInfo, am, invokeParams, true, cancellationTokenSource.Token);
					am.GetCanvasEditorElementPath = null;
					ac.Item = null;
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
				: ("json", Client.Serialize(methodResult, true));
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
					attachment.SendType = AttachmentSendType.None;
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

		public static List<ChatTool> GetChatToolDefinitions(TemplateItem item)
		{
			var tools = new List<ChatTool>();
			if (!item.PluginsEnabled)
				return tools;
			foreach (var pluginItem in GetPluginFunctions())
			{
				var maxRiskLevel = (RiskLevel)Math.Min((int)item.MaxRiskLevel, (int)AppHelper.GetMaxRiskLevel());
				if (!AllowPluginFunction(pluginItem.Name, maxRiskLevel))
					continue;
				// If this is not a Visual Studio extension but a plugin for Visual Studio, then skip.
				if (!Global.IsVsExtension && pluginItem.Class == nameof(VisualStudio))
					continue;
				// Get Method Info
				var mi = pluginItem.Mi;
				if (mi != null)
				{
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
						pluginItem.Name,
						string.Join("\r\n\r\n", lines),
						binaryParamaters
					);
					// If avatar voice not enabled then disable.
					var useVoice = item.UseAvatarVoice || Global.IsAvatarInWindow || item.ShowAvatar;
					var voiceFunctions = new[] { nameof(Multimedia.AISpeak), nameof(Multimedia.PlayText), nameof(Multimedia.StopText) };
					if (!useVoice && voiceFunctions.Contains(tool.FunctionName))
						continue;
					tools.Add(tool);
				}
			}
			return tools;
		}

		/// <summary>
		/// Add completion tools to chat completion message request for OpenAI GPT.
		/// </summary>
		/// <param name="item">Template item with settings.</param>
		/// <param name="options">Chat completion options</param>
		public static void ProvideTools(List<ChatTool> tools, TemplateItem item,
			ChatCompletionOptions options = null, MessageItem message = null)
		{
			if (tools.Count == 0)
				return;
			// Need to use reflection to set the Temperature property
			// because the developers used unnecessary C# 9.0 features that won't work on .NET 4.8.
			var value = ChatToolChoice.CreateAutoChoice();
			var toolsToProvide = new List<ChatTool>();
			// Make sure that last message is not automated reply or it will go into the infinite loop.
			if (item.Messages.LastOrDefault()?.IsAutomated == true)
				return;
			// If chat is configured to require a specific tool then...
			if (item.ToolChoiceRequired)
			{
				value = ChatToolChoice.CreateRequiredChoice();
				var requiredFunctions = tools.Where(x => item.ToolChoiceRequiredNames.Contains(x.FunctionName)).ToList();
				foreach (var tool in requiredFunctions)
					toolsToProvide.Add(tool);
			}
			// If no required tools are added, then...
			if (toolsToProvide.Count == 0)
			{
				// Add all functions as available.
				foreach (var tool in tools)
					toolsToProvide.Add(tool);
			}
			// If options supplied then..
			if (options != null)
			{
				toolsToProvide.ForEach(x => options.Tools.Add(x));
				typeof(ChatCompletionOptions)
					.GetProperty(nameof(ChatCompletionOptions.ToolChoice), BindingFlags.Public | BindingFlags.Instance)
						?.SetValue(options, value, null);
			}
			if (message != null)
			{
				var functions = Client.ConvertChatToolsTo(toolsToProvide);
				var functionDefinitions = MarkdownHelper.CreateMarkdownCodeBlock(Client.Serialize(functions), "json");
				// Create function definitions attachment.
				var fda = new MessageAttachments();
				fda.Title = "Function Definitions";
				fda.Instructions = Global.AppSettings.ContextFunctionRequestInstructions;
				fda.Type = ContextType.None;
				fda.Data = functionDefinitions;
				fda.SendType = AttachmentSendType.Temp;
				message.Attachments.Add(fda);
				// Create response definitions attachment.
				var settings = new SystemTextJsonSchemaGeneratorSettings
				{
					SchemaType = NJsonSchema.SchemaType.OpenApi3,          // Use OpenAPI v3 schema
					GenerateExamples = true,                               // Generate examples from XML comments
					UseXmlDocumentation = true,                            // Include XML documentation
					ResolveExternalXmlDocumentation = true,                // Resolve XML documentation from external assemblies
					XmlDocumentationFormatting = XmlDocsFormattingMode.Markdown,  // Format XML docs as Markdown
					AllowReferencesWithProperties = true,                  // Allow $ref with additional properties
					FlattenInheritanceHierarchy = false,                   // Use allOf for inheritance
					GenerateAbstractSchemas = true,                        // Include abstract schemas
					AlwaysAllowAdditionalObjectProperties = false,         // Do not allow additional properties by default
				};
				var responseSchema = NJsonSchema.JsonSchema.FromType(typeof(IEnumerable<chat_completion_message_tool_call>), settings);
				var responseDefinition = MarkdownHelper.CreateMarkdownCodeBlock(responseSchema.ToJson(), "json");
				var rda = new MessageAttachments();
				rda.Title = "Function Call Definition";
				rda.Instructions = Global.AppSettings.ContextFunctionResponseInstructions;
				rda.Type = ContextType.None;
				rda.Data = responseDefinition;
				rda.SendType = AttachmentSendType.Temp;
				message.Attachments.Add(rda);
			}
		}

		#endregion

		#region Function calls inside assistant message

		public static Match[] GetMarkdownMatches(string name, string text)
		{
			// Pattern to find blocks enclosed in triple backticks
			var pattern = @"```(?:" + name + @")?\s*\n([\s\S]*?)\n```";
			var regex = new Regex(pattern, RegexOptions.IgnoreCase);
			var matches = regex.Matches(text).Cast<Match>().ToArray();
			return matches;
		}

		public static (string assistantMessage, chat_completion_function[] calls) ProcessAssistantMessage(string assistantMessage)
		{
			var functionCalls = new List<chat_completion_function>();
			var matches = GetMarkdownMatches("json", assistantMessage);
			foreach (Match match in matches)
			{
				var jsonText = match.Groups[1].Value.Trim();
				// Parse the extracted JSON text
				try
				{
					using (JsonDocument doc = JsonDocument.Parse(jsonText))
					{
						// Root element can contain `chat_completion_function` or `chat_completion_function[]`.
						var rootElement = doc.RootElement;
						var items = new List<JsonElement>();
						if (rootElement.ValueKind == JsonValueKind.Object)
						{
							items.Add(rootElement);
						}
						else if (rootElement.ValueKind == JsonValueKind.Array)
						{
							var elements = rootElement.EnumerateArray().ToArray();
							if (elements.Any(x => x.ValueKind != JsonValueKind.Object))
							{
								Console.WriteLine("Not all elements in the JSON array are of Object kind.");
								continue;
							}
							items.AddRange(elements);
						}
						foreach (var item in items)
						{
							// Check if the JSON object represents a tool function call.
							if (!IsFunctionCallJson(item))
								continue;
							var toolCall = Client.Deserialize<chat_completion_message_tool_call>(item.ToString());
							var function = new chat_completion_function
							{
								id = toolCall.id,
								name = toolCall.function.name,
								parameters = toolCall.function.parameters,
							};
							functionCalls.Add(function);
						}
						// Remove the matched JSON block from the assistant message
						assistantMessage = assistantMessage.Replace(match.Value, "").Trim();
					}
				}
				catch (JsonException)
				{
					// If JSON parsing fails, skip this block but leave it in the message
					continue;
				}
			}
			return (assistantMessage, functionCalls.ToArray());
		}

		private static bool IsFunctionCallJson(JsonElement jsonElement)
		{
			// Check for required properties: id, type, and function
			if (jsonElement.TryGetProperty("id", out JsonElement idElement) &&
				jsonElement.TryGetProperty("type", out JsonElement typeElement) &&
				jsonElement.TryGetProperty("function", out JsonElement functionElement))
			{
				// Validate that id starts with "call_"
				if (idElement.ValueKind == JsonValueKind.String &&
					idElement.GetString().StartsWith("call_", StringComparison.Ordinal))
				{
					// Validate that type is "function"
					if (typeElement.ValueKind == JsonValueKind.String &&
						typeElement.GetString() == "function")
					{
						// Validate that function is an object
						if (functionElement.ValueKind == JsonValueKind.Object)
						{
							return true;
						}
					}
				}
			}
			return false;
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
				var parameters = function.parameters?.additional_properties;
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
			foreach (var pluginItem in GetPluginFunctions())
			{
				if (!AllowPluginFunction(pluginItem.Name, maxRiskLevel))
					continue;
				var mi = pluginItem.Mi;
				if (mi is null)
					continue;
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
