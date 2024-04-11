using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.DataClient;
using JocysCom.VS.AiCompanion.Engine.Companions;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using JocysCom.VS.AiCompanion.Plugins.Core;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JocysCom.VS.AiCompanion.Engine
{
	public static class AppHelper
	{
		public const int NavigateDelayMs = 250;
		public static void SetText(Label label, string name, int count, int updatable = 0)
		{
			var text = $"{count} {name}" + (count == 1 ? "" : "s");
			if (updatable > 0)
				text += $", {updatable} Updatable";
			ControlsHelper.SetText(label, text);
		}

		public static MacroValues GetMacroValues()
		{
			var mv = new MacroValues();
			if (Global.IsVsExtension)
				mv.Selection = Global._SolutionHelper.GetSelection();
			if (Global.IsVsExtension)
				mv.Document = Global._SolutionHelper.GetCurrentDocument(true) ?? new DocItem();
			return mv;
		}

		public static List<PropertyItem> GetReplaceMacrosSelection()
		{
			var keys = JocysCom.ClassLibrary.Text.Helper.GetReplaceMacros<DocItem>(true, nameof(MacroValues.Selection));
			return keys.Select(x => new PropertyItem(x)).ToList();
		}

		//public static List<string> GetMacrosOfStartupProject()
		//	=> Global.GetMacrosOfStartupProject().Keys.ToList();

		public static List<PropertyItem> GetReplaceMacrosDocument()
		{
			var keys = JocysCom.ClassLibrary.Text.Helper.GetReplaceMacros<DocItem>(true, nameof(MacroValues.Document));
			return keys.Select(x => new PropertyItem(x)).ToList();
		}

		public static List<PropertyItem> GetReplaceMacrosDate()
		{
			var keys = JocysCom.ClassLibrary.Text.Helper.GetReplaceMacros<DateTime>(true, nameof(MacroValues.Date));
			return keys.Select(x => new PropertyItem(x)).ToList();
		}

		private const string EnvironmentPrefix = "Env";

		public static string ReplaceMacros(string s, MacroValues o)
		{
			s = JocysCom.ClassLibrary.Text.Helper.Replace(s, o.Date, true, nameof(MacroValues.Date));
			s = JocysCom.ClassLibrary.Text.Helper.Replace(s, o.Selection, true, nameof(MacroValues.Selection));
			s = JocysCom.ClassLibrary.Text.Helper.Replace(s, o.Document, true, nameof(MacroValues.Document));
			var envDic = GetEnvironmentProperties().ToDictionary(x => x.Key.Substring(EnvironmentPrefix.Length + 1), x => (object)x.Value);
			s = JocysCom.ClassLibrary.Text.Helper.ReplaceDictionary(s, envDic, true, EnvironmentPrefix);
			return s;
		}

		public static string GetCodeFromReply(string replyText)
		{
			// Try to match code block pattern (triple backticks) with any language
			var codeBlockPattern = @"(?s)[\`]{3}(?<language>.*?)\r?\n(?<code>.*?)[\`]{3}";
			var codeBlockMatch = Regex.Match(replyText, codeBlockPattern, RegexOptions.Singleline);
			// If code block found, return the code inside it
			if (codeBlockMatch.Success)
				return codeBlockMatch.Groups["code"].Value.Trim();
			// Try to match inline code pattern (single backticks)
			var inlineCodePattern = @"`(?<code>.*?)`";
			var inlineCodeMatch = Regex.Match(replyText, inlineCodePattern);
			// If inline code found, return the code inside it
			if (inlineCodeMatch.Success)
				return inlineCodeMatch.Groups["code"].Value;
			// If no code block or inline code found, return the original reply text
			return replyText;
		}

		public static List<PropertyItem> GetEnvironmentProperties()
		{
			var envVars = Environment.GetEnvironmentVariables();
			var solutionProperties = new List<PropertyItem>();
			foreach (DictionaryEntry envVar in envVars)
			{
				if ($"{envVar.Key}".Contains("."))
					continue;
				var solutionProperty = new PropertyItem
				{
					Key = $"{EnvironmentPrefix}.{envVar.Key}",
					Value = $"{envVar.Value}",
					Display = $"{envVar.Value}",
					//Display = $"{envVar.Key} = {envVar.Value}"
				};
				solutionProperties.Add(solutionProperty);
			}
			return solutionProperties.OrderBy(x => x.Key).ToList();
		}

		public static DocItem GetClipboard()
		{
			var text = Clipboard.GetText();
			var item = new DocItem(text);
			item.Name = nameof(Clipboard);
			return item;
		}
		public static void SetClipboard(string text)
		{
			Clipboard.SetText(text);
		}


		public static List<chat_completion_message[]> GetMessageGroups(List<chat_completion_message> source)
		{
			var messageGroups = new List<chat_completion_message[]>();
			var group = new List<chat_completion_message>();
			foreach (var message in source)
			{
				group.Add(message);
				// Every group will be completed with an answer from the AI assistant.
				if (message.role == message_role.assistant)
				{
					messageGroups.Add(group.ToArray());
					group.Clear();
				}
			}
			// If no messages were added because no message ends with the assistant message, then add all messages.
			if (messageGroups.Count == 0 && source.Count > 0)
				messageGroups.Add(source.ToArray());
			return messageGroups;
		}

		/// <summary>
		/// Return list of messages, but do not exceed availableTokens.
		/// First message. All mesages beginning from the end.
		/// </summary>
		/// <param name="messages"></param>
		/// <param name="availableTokens"></param>
		public static List<chat_completion_message> GetMessages(
			List<chat_completion_message> messages,
			int availableTokens,
			JsonSerializerOptions serializerOptions
		)
		{
			var target = new List<chat_completion_message>();
			if (messages.Count == 0)
				return target;
			var groups = GetMessageGroups(messages);
			// Try to include first messages.
			var firstGroup = groups.First();
			availableTokens -= ClientHelper.CountTokens(firstGroup, serializerOptions);
			if (availableTokens < 0)
				return target;
			groups.Remove(firstGroup);
			target.AddRange(firstGroup);
			// Reverse order (begin adding latest messages groups first)
			groups.Reverse();
			var middleGroups = new List<chat_completion_message[]>();
			for (int i = 0; i < groups.Count; i++)
			{
				var group = groups[i];
				availableTokens -= ClientHelper.CountTokens(group, serializerOptions);
				if (availableTokens < 0)
					break;
				middleGroups.Add(group);
			}
			middleGroups.Reverse();
			target.AddRange(middleGroups.SelectMany(x => x));
			return target;
		}

		public static System.Drawing.Image ConvertDrawingImageToDrawingBitmap(DrawingImage drawingImage, int targetWidth, int targetHeight)
		{
			// Create a BitmapSource from the DrawingImage
			double dpi = 96;
			RenderTargetBitmap renderTarget = new RenderTargetBitmap(targetWidth, targetHeight, dpi, dpi, PixelFormats.Pbgra32);
			DrawingVisual drawingVisual = new DrawingVisual();

			using (DrawingContext context = drawingVisual.RenderOpen())
			{
				context.DrawImage(drawingImage, new Rect(new System.Windows.Point(), new System.Windows.Size(targetWidth, targetHeight)));
			}

			renderTarget.Render(drawingVisual);
			BitmapSource bitmapSource = BitmapFrame.Create(renderTarget);

			// Convert the BitmapSource to a Bitmap
			System.Drawing.Bitmap bitmap;
			using (MemoryStream outStream = new MemoryStream())
			{
				BitmapEncoder encoder = new PngBitmapEncoder();
				encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
				encoder.Save(outStream);
				bitmap = new System.Drawing.Bitmap(outStream);
			}

			return bitmap;
		}

		public static List<string> ExtractFilePaths(string stackTrace)
		{
			var items = new List<string>();
			if (string.IsNullOrEmpty(stackTrace))
				return items;
			var matchCollection = Regex.Matches(stackTrace, @"in\s(?<name>.*):line\s\d+");
			foreach (Match match in matchCollection)
			{
				var name = match.Groups["name"].Value;
				var isValid = !name.ToCharArray().Intersect(Path.GetInvalidPathChars()).Any();
				if (isValid)
					items.Add(name);
			}
			return items;
		}

		/// <summary>
		/// Fix name to make sure that it is not same as existing names.
		/// </summary>
		public static void FixName(ISettingsListFileItem copy, IBindingList items)
		{
			var newName = copy.Name;
			for (int i = 1; i < int.MaxValue; i++)
			{
				var sameFound = items.Cast<ISettingsListFileItem>().Any(x => string.Equals(x.Name, newName, StringComparison.OrdinalIgnoreCase));
				// If item with the same name not found then...
				if (!sameFound)
					break;
				// Change name of the copy and continue.
				newName = $"{copy.Name} ({i})";
				continue;
			}
			if (copy.Name != newName)
				copy.Name = newName;
		}

		public static string ContainsSensitiveData(string contents)
		{
			if (string.IsNullOrEmpty(contents))
				return null;
			List<string> sensitiveWords = new List<string> {
				"password",
				"card number",
				"secret keyword",
				"social security number",
				"credit card",
				"cvv",
				"expiration date",
				"passport number"
			};
			var lower = contents.ToLower();
			foreach (var word in sensitiveWords)
			{
				if (lower.Contains(word))
					return word;
			}
			return null;
		}

		/// <summary>
		/// Helps to generate same Unique IDs.
		/// </summary>
		public static Guid GetGuid(params object[] args)
		{
			var value = string.Join(Environment.NewLine, args);
			var algorithm = System.Security.Cryptography.SHA256.Create();
			// Important: Don’t Use Encoding.Default, because it is different on different machines and send data may be decoded as as gibberish.
			// Use UTF-8 or Unicode (UTF-16), used by SQL Server.
			var encoding = Encoding.UTF8;
			var bytes = encoding.GetBytes(value);
			var hash = algorithm.ComputeHash(bytes);
			var guidBytes = new byte[16];
			Array.Copy(hash, guidBytes, guidBytes.Length);
			Guid guid = new Guid(guidBytes);
			algorithm.Dispose();
			return guid;
		}


		public static async Task UpdateModels(AiService aiService)
		{
			var isCtrlDown = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
			string[] modelCodes;
			if (isCtrlDown)
			{
				var box = new MessageBoxWindow();
				box.SetSize(640, 240);
				var serviceModels = Global.AppSettings.AiModels
					.Where(x => x.AiServiceId == aiService.Id)
					.Select(x => x.Name)
					.Concat(new string[] { aiService.DefaultAiModel })
					.Distinct()
					.ToList();
				var modelsText = string.Join("\r\n", serviceModels);
				var results = box.ShowPrompt(modelsText, "Set Models");
				if (results != MessageBoxResult.OK)
					return;
				var value = box.MessageTextBox.Text ?? "";
				modelCodes = value
					.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
					.Select(x => x.Trim())
					.ToArray();
			}
			else
			{
				// Download models from API service.
				if (!Global.IsGoodSettings(aiService, true))
					return;
				var client = new Client(aiService);
				var models = await client.GetModels();
				modelCodes = models?.Select(x => x.id).ToArray();
			}
			SetModelCodes(aiService, modelCodes);
		}

		public static void SetModelCodes(AiService aiService, string[] modelCodes, model[] models = null)
		{
			// If models found then...
			if (modelCodes?.Any() == true)
			{
				Regex filterRx = null;
				try
				{
					filterRx = new Regex(aiService.ModelFilter);
				}
				catch { }
				if (filterRx != null)
					modelCodes = modelCodes.Where(x => filterRx.IsMatch(x)).ToArray();
				// Remove all old models of AiService.
				var serviceModels = Global.AppSettings.AiModels.Where(x => x.AiServiceId == aiService.Id).ToList();
				foreach (var serviceModel in serviceModels)
					Global.AppSettings.AiModels.Remove(serviceModel);
				// Add all new models of AiService.
				foreach (var modelCode in modelCodes)
				{
					var aiModel = new AiModel(modelCode, aiService.Id);
					if (models != null)
					{
						// Detect if AI model can be finetuned.
						var model = models.FirstOrDefault(x => x.id == modelCode);
						aiModel.AllowFineTuning = GetPermission(model, "allow_fine_tuning") ?? false;
					}
					Global.AppSettings.AiModels.Add(aiModel);
				}
				// This will inform all forms that models changed.
				Global.RaiseOnAiModelsUpdated();
			}
		}

		public static bool? GetPermission(model model, string name)
		{
			JsonElement permissions;
			if (model.additional_properties == null)
				return null;
			if (!model.additional_properties.TryGetValue("permission", out permissions) || permissions.ValueKind != JsonValueKind.Array)
				return null;
			foreach (JsonElement element in permissions.EnumerateArray())
			{
				if (element.ValueKind != JsonValueKind.Object)
					continue;
				JsonElement allowFineTuning;
				if (element.TryGetProperty(name, out allowFineTuning))
				{
					bool value;
					if (bool.TryParse(allowFineTuning.GetRawText(), out value))
						return value;
				}
			}
			return null;
		}

		/// <summary>
		/// Load models ComboBoc source.
		/// </summary>
		/// <param name="extraNames">
		/// Make sure that target list contains extra model names.
		/// </param>
		public static void UpdateModelCodes(AiService aiService, IList<string> target, params string[] extraNames)
		{
			if (aiService == null)
				return;
			// Make sure checkbox can display current model.
			var serviceModels = Global.AppSettings.AiModels
				.Where(x => x.AiServiceId == aiService.Id)
				.Select(x => x.Name)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
			foreach (var extraName in extraNames)
			{
				if (!string.IsNullOrEmpty(extraName) && !serviceModels.Contains(extraName))
					serviceModels.Add(extraName);
			}
			CollectionsHelper.Synchronize(serviceModels, target);
		}

		public static TemplateItem GetNewTemplateItem(bool setNameAndIcon = false)
		{
			var item = new TemplateItem();
			item.Created = DateTime.Now;
			item.Modified = item.Created;
			var defaultAiService = Global.AppSettings.AiServices.FirstOrDefault(x => x.IsDefault) ??
				Global.AppSettings.AiServices.FirstOrDefault();
			item.AiServiceId = defaultAiService?.Id ?? Guid.Empty;
			item.AiModel = defaultAiService?.DefaultAiModel;
			if (setNameAndIcon)
			{
				item.Name = $"Template_{DateTime.Now:yyyyMMdd_HHmmss}";
				// Set default icon. Make sure "document_gear.svg" Build Action is Embedded resource.
				var contents = Helper.FindResource<string>(
					Resources.Icons.Icons_Default.Icon_document_gear.Replace("Icon_", "") + ".svg",
					typeof(AppHelper).Assembly);
				item.SetIcon(contents);
			}
			return item;
		}

		public static FineTuningItem GetNewFineTuningItem()
		{
			var item = new FineTuningItem();
			item.Created = DateTime.Now;
			item.Modified = item.Created;
			var defaultAiService = Global.AppSettings.AiServices.FirstOrDefault(x => x.IsDefault) ??
				Global.AppSettings.AiServices.FirstOrDefault();
			item.AiServiceId = defaultAiService?.Id ?? Guid.Empty;
			item.AiModel = defaultAiService.DefaultAiModel ?? "gpt-3.5-turbo";
			item.Name = $"FineTuning {DateTime.Now:yyyyMMdd_HHmmss}";
			// Set default icon. Make sure "control_panel.svg" Build Action is Embedded resource.
			var contents = Helper.FindResource<string>(
				Resources.Icons.Icons_Default.Icon_control_panel.Replace("Icon_", "") + ".svg",
				typeof(AppHelper).Assembly);
			item.SetIcon(contents);
			return item;
		}

		public static AssistantItem GetNewAssistantItem()
		{
			var item = new AssistantItem();
			item.Created = DateTime.Now;
			item.Modified = item.Created;
			var defaultAiService = Global.AppSettings.AiServices.FirstOrDefault(x => x.IsDefault) ??
				Global.AppSettings.AiServices.FirstOrDefault();
			item.AiServiceId = defaultAiService?.Id ?? Guid.Empty;
			item.AiModel = defaultAiService.DefaultAiModel ?? "gpt-3.5-turbo";
			item.Name = $"Assistant {DateTime.Now:yyyyMMdd_HHmmss}";
			// Set default icon. Make sure "control_panel.svg" Build Action is Embedded resource.
			var contents = Helper.FindResource<string>(
				Resources.Icons.Icons_Default.Icon_user_comment.Replace("Icon_", "") + ".svg",
				typeof(AppHelper).Assembly);
			item.SetIcon(contents);
			return item;
		}

		public static ListInfo GetNewListsItem()
		{
			var item = new ListInfo();
			item.Created = DateTime.Now;
			item.Modified = item.Created;
			item.Name = $"List {DateTime.Now:yyyyMMdd_HHmmss}";
			SetIconToDefault(item);
			return item;
		}

		public static void SetIconToDefault(ListInfo item)
		{
			// Set default icon. Make sure "control_panel.svg" Build Action is Embedded resource.
			var contents = Helper.FindResource<string>(
				Resources.Icons.Icons_Default.Icon_list.Replace("Icon_", "") + ".svg",
				typeof(AppHelper).Assembly);
			item.SetIcon(contents);
		}

		public static EmbeddingsItem GetNewEmbeddingsItem()
		{
			var item = new EmbeddingsItem();
			item.Created = DateTime.Now;
			item.Modified = item.Created;
			item.Name = $"Embedding {DateTime.Now:yyyyMMdd_HHmmss}";
			var defaultAiService = Global.AppSettings.AiServices.FirstOrDefault(x => x.IsDefault)
				?? Global.AppSettings.AiServices.FirstOrDefault();
			item.AiServiceId = defaultAiService?.Id ?? Guid.Empty;
			var models = Global.AppSettings.AiModels.Where(x => x.AiServiceId == defaultAiService?.Id);
			item.AiModel = models?.FirstOrDefault(x => x.Name.IndexOf("embedding", StringComparison.OrdinalIgnoreCase) >= 0)?.Name
				?? defaultAiService?.DefaultAiModel
				?? "text-embedding-3-large";
			item.Instructions = Resources.Resources.Embedding_Default_Instructions;
			item.Source = AssemblyInfo.ParameterizePath(Global.Embeddings.GetFileItemFullBaseName(item), true);
			item.Target = AssemblyInfo.ParameterizePath(Global.Embeddings.GetFileItemFullBaseName(item) + SqlInitHelper.SqliteExt, true);
			// Find free flag number.
			var taken = Global.Embeddings.Items.Select(x => x.EmbeddingGroupFlag)
				.Distinct();
			var free = Enum.GetValues(typeof(EmbeddingGroup))
				.Cast<EmbeddingGroup>().Except(taken).FirstOrDefault();
			item.EmbeddingGroupFlag = free;
			SetIconToDefault(item);
			return item;
		}

		public static void SetIconToDefault(EmbeddingsItem item)
		{
			// Set default icon. Make sure "control_panel.svg" Build Action is Embedded resource.
			var contents = Helper.FindResource<string>(
				Resources.Icons.Icons_Default.Icon_chart_radar.Replace("Icon_", "") + ".svg",
				typeof(AppHelper).Assembly);
			item.SetIcon(contents);
		}

		public static FineTuningItem GetNewFineTuning()
		{
			var item = new FineTuningItem();
			return item;
		}

		/// <summary>
		/// Set custom dictionary for spell check.
		/// </summary>
		/// <param name="box">TextBox</param>
		/// <param name="languages">Language list, for example: de-DE, en-GB.</param>
		public static void SetCustomDictionaryTextBox(TextBox box, IList<string> languages)
		{
			if (box.SpellCheck.IsEnabled && box.SpellCheck.CustomDictionaries.Count == 0)
			{
				box.SpellCheck.CustomDictionaries.Clear();
				foreach (var language in languages)
				{
					// The Uri points to the.lex file in the application root.
					var uri = new Uri($"pack://application:,,,/Dictionary.{language}.lex");
					box.SpellCheck.CustomDictionaries.Add(uri);
				}
			}
		}

		public static void AddHelp(Control control, string head, string body)
		{
			Global.MainControl.InfoPanel.HelpProvider.Add(control, head, body);
		}

		public static void AddHelp(Slider control, string help)
		{
			Global.MainControl.InfoPanel.HelpProvider.Add(control, control.Tag as string, help);
		}

		public static void AddHelp(ContentControl control, string help)
		{
			Global.MainControl.InfoPanel.HelpProvider.Add(control, control.Content as string, help);
		}

		#region Dialogs

		public static bool AllowAction(string actionName, params string[] args)
		{
			var names = string.Join("\r\n", args);
			var text = $"Do you want to {actionName.ToString().ToLower()} {args.Length} item{(args.Length > 1 ? "s" : "")}?";
			text += "\r\n\r\n";
			text += names;
			var caption = $"{Global.Info.Product} - {actionName}";
			var result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
			return result == MessageBoxResult.Yes;
		}


		public static bool AllowAction(AllowAction actionName, params string[] args)
		{
			return AllowAction(actionName.ToString(), args);
		}

		#endregion

		#region Copy Properties

		public static BindingFlags DefaultBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		public static bool IsKnownType(Type type)
		{
			if (type is null)
				throw new ArgumentNullException(nameof(type));
			return
				// Note: Every Primitive type (such as int, double, bool, char, etc.) is a ValueType. 
				type.IsPublic && (
					type == typeof(string) ||
					type.IsValueType ||
					// Type has parameterless constructor.
					type.GetConstructor(Type.EmptyTypes) != null
				);
		}

		/// <summary>Cache data for speed.</summary>
		/// <remarks>Cache allows for this class to work 20 times faster.</remarks>
		private static ConcurrentDictionary<Type, PropertyInfo[]> Properties { get; } = new ConcurrentDictionary<Type, PropertyInfo[]>();

		private static PropertyInfo[] GetProperties(Type t, bool cache = true)
		{
			var items = cache
				? Properties.GetOrAdd(t, x => t.GetProperties(DefaultBindingFlags))
				: t.GetProperties(DefaultBindingFlags);
			return items;
		}

		public static void CopyProperties(object source, object target)
		{
			if (source is null)
				throw new ArgumentNullException(nameof(source));
			if (target is null)
				throw new ArgumentNullException(nameof(target));
			// Get type of the destination object.
			var sourceProperties = GetProperties(source.GetType());
			var targetProperties = GetProperties(target.GetType());
			foreach (var sp in sourceProperties)
			{
				// Get destination property and skip if not found.
				var tp = targetProperties.FirstOrDefault(x => Equals(x.Name, sp.Name));
				if (!sp.CanRead || !tp.CanWrite)
					continue;
				if (tp == null || !IsKnownType(sp.PropertyType) || sp.PropertyType != tp.PropertyType)
					continue;
				// If type is not value type and has parameterless constructor.
				var useJson = !sp.PropertyType.IsValueType && sp.PropertyType.IsPublic && sp.PropertyType.GetConstructor(Type.EmptyTypes) != null;
				// Get source value.
				var sValue = sp.GetValue(source, null);
				if (useJson)
					sValue = JsonSerializer.Serialize(sValue);
				var update = true;
				// If can read target value.
				if (tp.CanRead)
				{
					// Get target value.
					var dValue = tp.GetValue(target, null);
					if (useJson)
						dValue = JsonSerializer.Serialize(dValue);
					// Update only if values are different.
					update = !Equals(sValue, dValue);
				}
				if (update)
				{
					if (useJson)
						sValue = JsonSerializer.Deserialize(sValue as string, tp.PropertyType);
					tp.SetValue(target, sValue, null);
				}
			}
		}

		#endregion

		#region Keep Focus on TextBox

		public static IInputElement lastFocusedElement;
		private static void Control_PreviewMouseDown(object sender, MouseButtonEventArgs e)
		{
			// Save the currently focused input element (if TextBox only).
			lastFocusedElement = Keyboard.FocusedElement as TextBox;
		}
		private static void Control_PreviewMouseUp(object sender, MouseButtonEventArgs e)
		{
			// Enqueue the action on the dispatcher.
			// This will ensure the action will be executed after the UI has finished processing events.
			Application.Current.Dispatcher.BeginInvoke((Action)(() =>
				lastFocusedElement?.Focus()));
		}

		public static void EnableKeepFocusOnMouseClick(params UIElement[] controls)
		{
			foreach (var control in controls)
			{
				control.PreviewMouseDown += Control_PreviewMouseDown;
				control.PreviewMouseUp += Control_PreviewMouseUp;
			}
		}

		public static void DisableKeepFocusOnMouseClick(params UIElement[] controls)
		{
			foreach (var control in controls)
			{
				control.PreviewMouseDown -= Control_PreviewMouseDown;
				control.PreviewMouseUp -= Control_PreviewMouseUp;
			}
		}

		#endregion

		#region TextBox Functions

		public static void SetText(TextBox box, string s)
		{
			int caretIndex = box.CaretIndex;
			// trim end and leave caret position unchanged
			box.Text = s;
			SetCaret(box, caretIndex);
		}

		public static void SetCaret(TextBox box, int index)
		{
			box.CaretIndex = index < box.Text.Length ? index : box.Text.Length;
		}

		public static void InsertText(TextBox box, string s, bool activate = false, bool addSpace = false)
		{
			// Check if we need to set the control active
			if (activate)
				box.Focus();
			// Save the current position of the cursor
			var cursorPosition = box.CaretIndex;
			// Check if there is a selected text to replace
			if (box.SelectionLength > 0)
			{
				// Replace the selected text
				box.SelectedText = s;
			}
			else
			{
				// If cursor at the end
				if (box.Text.Length > 0 && box.Text.Last() != ' ' && cursorPosition == box.Text.Length && addSpace)
					s = " " + s;
				// Insert the text at the cursor position
				box.Text = box.Text.Insert(cursorPosition, s);
				// Set the cursor after the inserted text
				box.CaretIndex = cursorPosition + s.Length;
			}
		}

		#endregion

		#region ■ Extract Helper

		/// <summary>
		/// Extract resource files
		/// </summary>
		/// <param name="source">Resource prefix.</param>
		/// <param name="target">Target folder to extract.</param>
		/// <param name="overwrite">Overwrite files at target.</param>
		public static void ExtractFiles(string source, string target, Assembly assembly = null)
		{
			// Get list of resources to extract.
			assembly = assembly ?? Assembly.GetExecutingAssembly();
			var resourceName = assembly.GetManifestResourceNames().Where(x => x.EndsWith(source)).First();
			var sr = assembly.GetManifestResourceStream(resourceName);
			if (sr == null)
				return;
			var bytes = new byte[sr.Length];
			sr.Read(bytes, 0, bytes.Length);
			// Open an existing zip file for reading.
			var zip = ZipStorer.Open(sr, FileAccess.Read);
			// Read the central directory collection
			var dir = zip.ReadCentralDir();
			// Look for the desired file.
			foreach (ZipStorer.ZipFileEntry entry in dir)
			{
				var fileName = System.IO.Path.Combine(target, entry.FilenameInZip.Replace("/", "\\"));
				zip.ExtractFile(entry, fileName);
			}
			zip.Close();
		}

		public static ZipStorer GetZip(string source, Assembly assembly = null)
		{
			// Get list of resources to extract.
			assembly = assembly ?? Assembly.GetExecutingAssembly();
			var resourceName = assembly.GetManifestResourceNames().Where(x => x.EndsWith(source)).First();
			var sr = assembly.GetManifestResourceStream(resourceName);
			if (sr == null)
				return null;
			var bytes = new byte[sr.Length];
			sr.Read(bytes, 0, bytes.Length);
			// Open an existing zip file for reading.
			var zip = ZipStorer.Open(sr, FileAccess.Read);
			return zip;
		}

		public static byte[] ExtractFile(string source, string filenameInZip, Assembly assembly = null)
		{
			var zip = GetZip(source, assembly);
			var bytes = ExtractFile(zip, filenameInZip);
			zip.Close();
			return bytes;
		}

		public static byte[] ExtractFile(ZipStorer zip, string filenameInZip)
		{
			// Read the central directory collection
			var dir = zip.ReadCentralDir();
			// Look for the desired file.
			foreach (ZipStorer.ZipFileEntry entry in dir)
			{
				if (entry.FilenameInZip != filenameInZip)
					continue;
				byte[] file;
				if (zip.ExtractFile(entry, out file))
					return file;
			}
			return null;
		}

		static string GetDevConPath()
		{
			var paString = Environment.Is64BitOperatingSystem ? "x64" : "x86";
			return string.Format("devcon.{0}.exe", paString);
		}

		#endregion

		#region Controls Helper

		public static void ShowButtons(Panel panel, params Button[] args)
		{
			var controls = ControlsHelper.GetAll<Button>(panel);
			foreach (var control in controls)
				control.Visibility = args.Contains(control) ? Visibility.Visible : Visibility.Collapsed;
		}

		public static bool IsGridInEditMode(DataGrid grid)
		{
			if (grid == null)
				return false;
			foreach (var item in grid.Items)
			{
				var row = grid.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
				if (row != null && row.IsEditing)
					return true;
			}
			return false;
		}

		#endregion

		#region Helper

		/// <summary>Built-in types</summary>
		public static readonly Dictionary<Type, string> TypeAliases = new Dictionary<Type, string>
		{
			{ typeof(bool), "bool" },
			{ typeof(byte), "byte" },
			{ typeof(char), "char" },
			{ typeof(decimal), "decimal" },
			{ typeof(double), "double" },
			{ typeof(float), "float" },
			{ typeof(int), "int" },
			{ typeof(long), "long" },
			{ typeof(object), "object" },
			{ typeof(sbyte), "sbyte" },
			{ typeof(short), "short" },
			{ typeof(string), "string" },
			{ typeof(uint), "uint" },
			{ typeof(ulong), "ulong" },
			{ typeof(ushort), "ushort" },
			{ typeof(void), "void" }
		};

		public static string GetBuiltInTypeNameOrAlias(Type type)
		{
			if (type is null)
				throw new ArgumentNullException(nameof(type));
			var elementType = type.IsArray
				? type.GetElementType()
				: type;
			// Lookup alias for type
			string alias;
			if (TypeAliases.TryGetValue(elementType, out alias))
				return alias + (type.IsArray ? "[]" : "");
			// Note: All Nullable<T> are value types.
			if (type.IsValueType)
			{
				var underType = Nullable.GetUnderlyingType(type);
				if (underType != null)
					return GetBuiltInTypeNameOrAlias(underType) + "?";
			}
			if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
			{
				var itemType = type.GetGenericArguments()[0];
				return string.Format("List<{0}>", GetBuiltInTypeNameOrAlias(itemType));
			}
			// Default to CLR type name
			return type.Name;
		}

		#endregion

	}

}
