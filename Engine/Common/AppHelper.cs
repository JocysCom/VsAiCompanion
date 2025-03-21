﻿using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Files;
using JocysCom.VS.AiCompanion.DataClient;
using JocysCom.VS.AiCompanion.DataClient.Common;
using JocysCom.VS.AiCompanion.Engine.Companions;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using JocysCom.VS.AiCompanion.Engine.Security;
using JocysCom.VS.AiCompanion.Plugins.Core;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;

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


		public static string GetTempFolderPath()
		{
			var path = Path.Combine(Global.AppData.XmlFile.Directory.FullName, "Temp");
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);
			return path;
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

		public static string XmlToColorizedHtml(string xml)
		{
			var formatter = new ColorCode.HtmlClassFormatter();
			var html = formatter.GetHtmlString(xml, ColorCode.Languages.Xml);
			// Get the CSS styles used by the formatter
			var styles = formatter.GetCSSString();
			// Embed the styles in a <style> tag
			html = $"<style>{styles}</style>{html}";
			return html;
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

		public static void SetClipboardHtml(string htmlContent)
		{
			// Adjust HTML content for clipboard
			var clipboardHtmlData = GetClipboardHtmlData(htmlContent);
			// Create a new data object
			var dataObject = new DataObject();
			var ext = Path.GetExtension(".html");
			var format = Mime.GetMimeContentType(ext);
			dataObject.SetData(format, htmlContent);
			// Allows to paste in text editors.
			dataObject.SetData(DataFormats.UnicodeText, htmlContent);
			// Set the HTML format in the DataObject (can be pasted into MS Word)
			dataObject.SetData(HtmlClipboardFormat, clipboardHtmlData);
			// Set the DataObject to the clipboard
			Clipboard.SetDataObject(dataObject, true);
		}

		private const string HtmlClipboardFormat = "HTML Format";

		private static string GetClipboardHtmlData(string htmlContent)
		{
			// Define the required header and footer for clipboard HTML
			var Header = "";
			Header += "Version:0.9\r\n";
			Header += "StartHTML:{0:00000000}\r\n";
			Header += "EndHTML:{1:00000000}\r\n";
			Header += "StartFragment:{2:00000000}\r\n";
			Header += "EndFragment:{3:00000000}\r\n";
			var HtmlStartFragment = "<!--StartFragment-->";
			var HtmlEndFragment = "<!--EndFragment-->";
			// Wrap the HTML content with the start and end fragment comments
			var completeHtml = HtmlStartFragment + htmlContent + HtmlEndFragment;
			// Calculate the lengths for header formatting
			int startHtml = Header.Length - "{0:00000000}".Length * 4;
			int startFragment = startHtml + completeHtml.IndexOf(HtmlStartFragment);
			int endFragment = startHtml + completeHtml.IndexOf(HtmlEndFragment) + HtmlEndFragment.Length;
			int endHtml = startHtml + completeHtml.Length;
			// Format the header with the calculated lengths
			var header = string.Format(Header, startHtml, endHtml, startFragment, endFragment);
			// Combine the header with the complete HTML content
			var result = header + completeHtml;
			return result;
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
				var serviceModels = Global.AiModels.Items
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
				if (!await Global.IsGoodSettings(aiService, true))
					return;
				var client = AiClientFactory.GetAiClient(aiService);
				if (client is null)
					return;
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
					if (!string.IsNullOrEmpty(aiService.ModelFilter))
						filterRx = new Regex(aiService.ModelFilter);
				}
				catch { }
				if (filterRx != null)
					modelCodes = modelCodes.Where(x => filterRx.IsMatch(x)).ToArray();
				// Remove all old models of AiService.
				var serviceModels = Global.AiModels.Items.Where(x => x.AiServiceId == aiService.Id).ToList();
				foreach (var serviceModel in serviceModels)
					Global.AiModels.Items.Remove(serviceModel);
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
					if (aiModel.MaxInputTokens == 0)
						aiModel.MaxInputTokens = Client.GetMaxInputTokens(aiModel.Name);
					Client.SetModelFeatures(aiModel);
					Global.AiModels.Add(aiModel);
				}
				Global.AppSettings.CleanupAiModels();
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
			var serviceModels = Global.AiModels.Items
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
			var defaultAiService = Global.AiServices.Items.FirstOrDefault(x => x.IsDefault) ??
				Global.AiServices.Items.FirstOrDefault();
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

		public static AiService GetNewAiService()
		{
			var item = new AiService();
			item.Name = $"Service_{DateTime.Now:yyyyMMdd_HHmmss}";
			return item;
		}

		public static AiModel GetNewAiModel()
		{
			var item = new AiModel();
			item.Name = $"Model_{DateTime.Now:yyyyMMdd_HHmmss}";
			return item;
		}

		public static FineTuningItem GetNewFineTuningItem()
		{
			var item = new FineTuningItem();
			item.Created = DateTime.Now;
			item.Modified = item.Created;
			var defaultAiService = Global.AiServices.Items.FirstOrDefault(x => x.IsDefault) ??
				Global.AiServices.Items.FirstOrDefault();
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
			var defaultAiService = Global.AiServices.Items.FirstOrDefault(x => x.IsDefault) ??
				Global.AiServices.Items.FirstOrDefault();
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

		public static UiPresetItem GetNewUiPresetItem()
		{
			var item = new UiPresetItem();
			item.Created = DateTime.Now;
			item.Modified = item.Created;
			item.Name = $"List {DateTime.Now:yyyyMMdd_HHmmss}";
			// Set default icon. Make sure "control_panel.svg" Build Action is Embedded resource.
			var contents = Helper.FindResource<string>(
				Resources.Icons.Icons_Default.Icon_list.Replace("Icon_", "") + ".svg",
				typeof(AppHelper).Assembly);
			item.SetIcon(contents);
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
			var defaultAiService = Global.AiServices.Items.FirstOrDefault(x => x.IsDefault)
				?? Global.AiServices.Items.FirstOrDefault();
			item.AiServiceId = defaultAiService?.Id ?? Guid.Empty;
			var models = Global.AiModels.Items.Where(x => x.AiServiceId == defaultAiService?.Id);
			item.AiModel = models?.FirstOrDefault(x => x.Name.IndexOf("embedding", StringComparison.OrdinalIgnoreCase) >= 0)?.Name
				?? defaultAiService?.DefaultAiModel
				?? "text-embedding-3-large";
			item.Instructions = Resources.MainResources.main_Embedding_Default_Instructions;
			item.Source = AssemblyInfo.ParameterizePath(Global.Embeddings.GetFileItemFullBaseName(item), true);
			item.Target = AssemblyInfo.ParameterizePath(Global.Embeddings.GetFileItemFullBaseName(item) + SqlInitHelper.SqliteExt, true);
			// Find free flag number.
			var taken = Global.Embeddings.Items.Select(x => x.EmbeddingGroupFlag)
				.Distinct();
			var free = Enum.GetValues(typeof(EmbeddingGroupFlag))
				.Cast<EmbeddingGroupFlag>().Except(taken).FirstOrDefault();
			item.EmbeddingGroupFlag = free;
			SetIconToDefault(item);
			return item;
		}

		public static MailAccount GetNewMailAccount()
		{
			var item = new MailAccount();
			item.Name = $"name@domain.com {DateTime.Now:yyyyMMdd_HHmmss}";
			item.Created = DateTime.Now;
			item.Modified = item.Created;
			return item;
		}

		public static VaultItem GetNewVaultItem()
		{
			var item = new VaultItem();
			item.Id = Guid.NewGuid();
			item.Name = $"VaultItem {DateTime.Now:yyyyMMdd_HHmmss}";
			item.Created = DateTime.Now;
			item.Modified = item.Created;
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
					var uri = AppHelper.GetResourceUri($"Dictionary.{language}.lex");
					box.SpellCheck.CustomDictionaries.Add(uri);
				}
			}
		}

		public static Uri GetResourceUri(string path, Assembly assembly = null)
		{
			assembly = assembly ?? Assembly.GetExecutingAssembly();
			// Local Assembly Resource File
			var resourcePath = $"pack://application:,,,/{assembly.GetName().Name};component/{path}";
			var uri = new Uri(resourcePath, UriKind.RelativeOrAbsolute);
			return uri; 
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


		public static void InitHelp(FrameworkElement root)
		{
			var elements = GetDirectElementsWithAutomationProperties(root);
			AddHelp(elements);
		}

		private static void AddHelp(params UIElement[] elements)
		{
			foreach (var element in elements)
			{
				var helpText = AutomationProperties.GetHelpText(element);
				var name = AutomationProperties.GetName(element);
				Global.MainControl.InfoPanel.HelpProvider.Add(element, name, helpText);
			}
		}

		public static void UpdateHelp(UIElement element, string name, string helpText)
		{
			var hp = Global.MainControl?.InfoPanel.HelpProvider;
			// If form not loaded yet then..
			if (hp is null)
				return;
			Global.MainControl.InfoPanel.HelpProvider.Remove(element);
			AutomationProperties.SetName(element, name);
			AutomationProperties.SetHelpText(element, helpText);
			Global.MainControl.InfoPanel.HelpProvider.Add(element, name, helpText);
		}

		/// <summary>
		/// Get all child controls. Excludes controls that are sourced from external XAML resource dictionaries.
		/// </summary>
		/// <param name="root">The root control.</param>
		/// <returns>A list of all child controls.</returns>
		private static FrameworkElement[] GetDirectElementsWithAutomationProperties(DependencyObject root)
		{
			if (root == null)
				throw new ArgumentNullException(nameof(root));
			var elements = new List<FrameworkElement>();
			GetAllInternal(root, elements);
			var elementsWithAutomation = elements
				.Where(x =>
					!string.IsNullOrEmpty(AutomationProperties.GetHelpText(x)) ||
					!string.IsNullOrEmpty(AutomationProperties.GetName(x)))
				.ToArray();
			return elementsWithAutomation;
		}

		public static void GetAllInternal(DependencyObject parent, List<FrameworkElement> elements)
		{
			if (parent == null)
				return;
			// Process logical children to support elements even if they're not loaded or visible
			var visualChildren = LogicalTreeHelper
				.GetChildren(parent)
				.OfType<FrameworkElement>()
				.ToArray();
			// Process visual children to support elements that are part of the visual tree
			var logicalChildren = Enumerable.Range(0, VisualTreeHelper.GetChildrenCount(parent))
				.Select(x => VisualTreeHelper.GetChild(parent, x))
				.OfType<FrameworkElement>()
				.ToArray();
			var children = visualChildren.Union(logicalChildren).ToArray();
			foreach (var child in children)
			{
				// Avoid duplicating elements that are already processed in logical tree.
				if (elements.Contains(child))
					continue;
				// Skip if element is part of external resource dictionaries.
				if (child.Resources.MergedDictionaries.OfType<ResourceDictionary>().Any(rd => rd.Source != null))
					continue;
				elements.Add(child);
				GetAllInternal(child, elements);
			}
		}

		#region Get Data

		public static List<ListInfo> GetListNames(string[] paths, params string[] prefix)
		{
			var items = Global.Lists.Items
				.Where(x => string.IsNullOrWhiteSpace(x.Path) || paths.Contains(x.Path))
				.OrderBy(x => $"{x.Path}")
				// Items with prefix on top.
				.ThenBy(x => prefix.Any(p => x.Name.StartsWith(p, StringComparison.OrdinalIgnoreCase)) ? 0 : 1)
				.ThenBy(x => x.Name)
				.ToList();
			items.Insert(0, new ListInfo());
			return items;
		}

		#endregion

		#region Encrypt and Decrypt

		/// <summary>
		/// Encrypt data. The protected data is associated with the current user.
		/// Only threads running under the current user context can unprotect the data.
		/// </summary>
		public static byte[] UserEncrypt(byte[] data)
		{
			if (data == null)
				return null;
			try
			{
				var user = "AppContext";
				return JocysCom.ClassLibrary.Security.Encryption.Encrypt(data, user);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}
			return null;
		}

		/// <summary>
		/// Encrypt data. The protected data is associated with the current user.
		/// Only threads running under the current user context can unprotect the data.
		/// </summary>
		public static string UserEncrypt(string text)
		{
			if (string.IsNullOrEmpty(text))
				return text;
			var decryptedData = Encoding.Unicode.GetBytes(text);
			var encryptedData = UserEncrypt(decryptedData);
			var encryptedText = Convert.ToBase64String(encryptedData);
			return encryptedText;
		}

		/// <summary>
		/// Decrypt data. The protected data is associated with the current user.
		/// Only threads running under the current user context can unprotect the data.
		/// </summary>
		public static byte[] UserDecrypt(byte[] data)
		{
			if (data == null)
				return null;
			try
			{
				var salt = "AppContext";
				return JocysCom.ClassLibrary.Security.Encryption.Decrypt(data, salt);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}
			return null;
		}

		/// <summary>
		/// Decrypt data. The protected data is associated with the current user.
		/// Only threads running under the current user context can unprotect the data.
		/// </summary>
		public static string UserDecrypt(string base64)
		{
			if (string.IsNullOrEmpty(base64))
				return base64;
			var encryptedData = Convert.FromBase64String(base64);
			var decryptedData = UserDecrypt(encryptedData);
			if (decryptedData == null)
				return null;
			var decryptedText = Encoding.Unicode.GetString(decryptedData);
			return decryptedText;
		}

		#endregion

		#region Global Max Risk Level

		public static RiskLevel GetMaxRiskLevel()
		{
			// Use max risk level set by all.
			var maxRiskLevel = Global.AppSettings.MaxRiskLevel;
			var profile = Global.UserProfile;
			if (profile.IsSignedIn && profile.UserGroups != null)
			{
				var userMaxRiskLevel = GetMaxRiskLevelByGroups(profile.UserGroups);
				if (userMaxRiskLevel == RiskLevel.Unknown)
					userMaxRiskLevel = Global.AppSettings.MaxRiskLevelWhenSignedOut;
				maxRiskLevel = (RiskLevel)Math.Min(
					(int)maxRiskLevel,
					(int)userMaxRiskLevel);
			}
			else
			{
				maxRiskLevel = (RiskLevel)Math.Min(
					(int)maxRiskLevel,
					(int)Global.AppSettings.MaxRiskLevelWhenSignedOut);
			}
			var domainMaxRiskLevel = DomainHelper.GetDomainUserMaxRiskLevel();
			// If domain max risk level exsits and it is more restrictive then use it.
			if (domainMaxRiskLevel.HasValue && domainMaxRiskLevel.Value < maxRiskLevel)
				maxRiskLevel = domainMaxRiskLevel.Value;
			return maxRiskLevel;
		}


		/// <summary>
		/// Get risk groups of the user.
		/// </summary>
		public static RiskLevel GetMaxRiskLevelByGroups(IList<string> groups)
		{
			var dic = GetLevels();
			var keys = dic.Keys.ToArray();
			foreach (var level in keys)
			{
				var groupName = GetGroupName(level);
				var exists = groups.Any(g => g.Equals(groupName, StringComparison.OrdinalIgnoreCase));
				dic[level] = exists;
			}
			// Get user maximum risk level.
			var maxRiskLevel = dic
				.Where(x => x.Value)
				.OrderByDescending(x => x.Key)?
				.FirstOrDefault().Key ?? RiskLevel.None;
			return maxRiskLevel;
		}
		public static Dictionary<RiskLevel, bool> GetLevels()
		{
			var dic = ((RiskLevel[])Enum.GetValues(typeof(RiskLevel))).Except(new RiskLevel[] { RiskLevel.Unknown })
				.ToDictionary(x => x, x => false);
			return dic;
		}

		public static string GetGroupName(RiskLevel level)
			=> $"AI_{nameof(RiskLevel)}_{level}";

		public static string[] GetGroupNames()
			=> GetLevels().Keys.Select(x => GetGroupName(x)).ToArray();

		#endregion

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

		public static bool AllowReset(string name, string more = "")
		{
			var text = $"Do you want to reset the {name}?";
			text += string.IsNullOrEmpty(more)
				? $" Please note that this will delete all custom {name}!"
				: more;
			var caption = $"{Global.Info.Product} - Reset {name}";
			var result = MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning);
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
			ControlsHelper.AppBeginInvoke(()
				=> lastFocusedElement?.Focus());
		}

		/// <summary>
		/// Used for checkboxes. Attempts to keep keyboard focus inside the textbox when the checkbox is clicked with the mouse.
		/// </summary>
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
			{
				box.Focus();
				Keyboard.Focus(box);
			}
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

		/// <summary>
		/// Use this function to make sure that the text pasted by the user doesn't have invalid characters that could crash XML serialization.
		/// </summary>
		public static string ReplaceInvalidXmlChars(string text)
		{
			if (string.IsNullOrEmpty(text))
				return text;
			var sb = new StringBuilder();
			foreach (var ch in text)
			{
				if (XmlConvert.IsXmlChar(ch))
					sb.Append(ch);
				else
					// Replace invalid character with its hexadecimal representation
					sb.AppendFormat("&#x{0:X};", (int)ch);
			}
			return sb.ToString();
		}

		#endregion

		#region ■ Extract Helper

		/// <summary>
		/// Extract resource files that match a specified regex pattern.
		/// </summary>
		/// <param name="source">Resource prefix.</param>
		/// <param name="targetPath">Target folder to extract.</param>
		/// <param name="searchPattern">Regex pattern to match file paths.</param>
		/// <param name="overwrite">Overwrite files at target.</param>
		/// <param name="assembly">Optional assembly to get resources from. Defaults to executing assembly.</param>
		public static void ExtractFiles(ZipStorer zip, string targetPath, string searchPattern = null)
		{
			var dir = zip.ReadCentralDir();
			Regex regex = string.IsNullOrEmpty(searchPattern) ? null : new Regex(searchPattern);
			foreach (ZipStorer.ZipFileEntry entry in dir)
			{
				var path = entry.FilenameInZip.Replace("/", "\\");
				if (regex != null && !regex.IsMatch(path))
					continue;
				var fileName = Path.Combine(targetPath, path);
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

		public static bool CollectionChanged(ListChangedEventArgs e, Action action, params string[] properties)
		{
			bool changed =
				e.ListChangedType == ListChangedType.Reset ||
				e.ListChangedType == ListChangedType.ItemDeleted ||
				e.ListChangedType == ListChangedType.ItemAdded ||
				e.ListChangedType == ListChangedType.ItemMoved;
			foreach (var property in properties)
				if (e.PropertyDescriptor?.Name == property)
					changed = true;
			if (changed)
				_ = Helper.Debounce(action);
			return changed;
		}

		/// <summary>
		/// Helps run cancellable methods of this form and logs results to the log panel.
		/// </summary>
		public static async Task<Exception> ExecuteMethod(ObservableCollection<CancellationTokenSource> tokens, Func<CancellationToken, Task> action)
		{
			Exception exception = null;
			var source = new CancellationTokenSource();
			source.CancelAfter(TimeSpan.FromSeconds(30));
			tokens?.Add(source);
			ControlsHelper.AppInvoke(() =>
			{
				Global.MainControl.InfoPanel.AddTask(source);
			});

			try
			{
				await action.Invoke(source.Token);
			}
			catch (Exception ex)
			{
				exception = ex;
				ControlsHelper.AppInvoke(() =>
				{
					Global.MainControl.InfoPanel.SetBodyError(ex.Message);
				});
			}
			finally
			{
				tokens?.Remove(source);
				ControlsHelper.AppInvoke(() =>
				{
					Global.MainControl.InfoPanel.RemoveTask(source);
				});
			}
			return exception;
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

		public static string GetNullIfWhiteSpace(string s) =>
			 string.IsNullOrWhiteSpace(s) ? null : s;

		#endregion

	}

}
