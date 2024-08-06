using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Runtime;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace JocysCom.VS.AiCompanion.Engine
{
	public static class SettingsSourceManager
	{

		public static void ResetAllSettings(bool confirm = false)
		{
			if (confirm && !AppHelper.AllowReset("All Settings", "Please note that this will reset all services, models, templates and tasks!"))
				return;
			try
			{
				var zip = GetSettingsZip();
				// Load data from a single XML file.
				var zipAppDataItems = GetItemsFromZip(zip, Global.AppDataName, Global.AppData);
				// Load data from multiple XML files.
				var zipTasks = GetItemsFromZip(zip, Global.TasksName, Global.Tasks);
				var zipTemplates = GetItemsFromZip(zip, Global.TemplatesName, Global.Templates);
				var zipLists = GetItemsFromZip(zip, Global.ListsName, Global.Lists);
				var zipEmbeddings = GetItemsFromZip(zip, Global.EmbeddingsName, Global.Embeddings);
				var zipAppSettings = zipAppDataItems[0];
				PreventWriteToNewerFiles(false);
				RemoveToReplace(Global.Tasks, zipTasks, x => x.Name);
				RemoveToReplace(Global.Templates, zipTemplates, x => x.Name);
				RemoveToReplace(Global.Lists, zipLists, x => x.Name);
				RemoveToReplace(Global.Embeddings, zipEmbeddings, x => x.Name);
				ResetServicesAndModels();
				ResetPrompts(zip);
				ResetVoices(zip);
				// Add user settings.
				Global.Embeddings.Add(zipEmbeddings.ToArray());
				Global.Lists.Add(zipLists.ToArray());
				Global.Templates.Add(zipTemplates.ToArray());
				Global.Tasks.Add(zipTasks.ToArray());
				// Copy other settings.
				RuntimeHelper.CopyProperties(zipAppSettings, Global.AppSettings, true);
				var settings = Global.AppSettings.PanelSettingsList.ToArray();
				foreach (var setting in settings)
				{
					var zipSetting = zipAppSettings.PanelSettingsList.FirstOrDefault(x => x.ItemType == setting.ItemType);
					if (zipSetting == null)
						continue;
					RuntimeHelper.CopyProperties(zipSetting, setting, true);
				}
				// Save settings.
				Global.SaveSettings();
				PreventWriteToNewerFiles(true);
				Global.RaiseOnAiServicesUpdated();
				Global.RaiseOnAiModelsUpdated();
				Global.RaiseOnTasksUpdated();
				Global.RaiseOnTemplatesUpdated();
				Global.RaiseOnListsUpdated();
			}
			catch (Exception ex)
			{
				Global.ShowError("ResetSettings() error: " + ex.Message);
			}
		}

		/// <summary>
		/// Allow overwriting newer files when saving current settings to the disk.
		/// </summary>
		static void PreventWriteToNewerFiles(bool enabled)
		{
			// Separate files.
			Global.Assistants.PreventWriteToNewerFiles = enabled;
			Global.Embeddings.PreventWriteToNewerFiles = enabled;
			Global.FineTunings.PreventWriteToNewerFiles = enabled;
			Global.Lists.PreventWriteToNewerFiles = enabled;
			Global.Tasks.PreventWriteToNewerFiles = enabled;
			Global.Templates.PreventWriteToNewerFiles = enabled;
			Global.UiPresets.PreventWriteToNewerFiles = enabled;
			// Single file.
			Global.AppData.PreventWriteToNewerFiles = enabled;
			Global.Prompts.PreventWriteToNewerFiles = enabled;
			Global.Voices.PreventWriteToNewerFiles = enabled;
		}

		public static Guid OpenAiServiceId
			=> AppHelper.GetGuid(nameof(AiService), OpenAiName);
		// Must be string constant or OpenAiServiceId property will get empty string.

		public const string OpenAiName = "Open AI";

		#region Reset App Settings

		/// <summary>
		/// Does not reset AiServices and AiModels.
		/// </summary>
		public static void ResetAppSettings(ZipStorer zip = null)
		{
			bool closeZip;
			if (closeZip = zip == null)
				zip = GetSettingsZip();
			if (zip == null)
				return;
			var zipAppDataItems = GetItemsFromZip(zip, Global.AppDataName, Global.AppData);
			if (zipAppDataItems == null)
				return;
			var zipAppSettings = zipAppDataItems[0];
			// Reset all app settings except of services, list of models and other reference types.
			JocysCom.ClassLibrary.Runtime.RuntimeHelper.CopyProperties(zipAppSettings, Global.AppSettings, true);
			// Close zip.
			if (closeZip)
				zip.Close();
		}

		/// <summary>Reset Tasks</summary>
		public static void ResetTasks(ZipStorer zip = null)
			=> ResetItems(zip, Global.Tasks, Global.AppSettings.ResetTasksMirror, Global.TasksName, x => x.Name);

		/// <summary>Reset Templates</summary>
		public static void ResetTemplates(ZipStorer zip = null)
			=> ResetItems(zip, Global.Templates, Global.AppSettings.ResetTemplatesMirror, Global.TemplatesName, x => x.Name);

		/// <summary>Reset Prompts</summary>
		public static void ResetPrompts(ZipStorer zip = null)
			=> ResetItems(zip, Global.Prompts, Global.AppSettings.ResetPromptsMirror, Global.PromptsName, x => x.Name);

		/// <summary>Reset Voices</summary>
		public static void ResetVoices(ZipStorer zip = null)
			=> ResetItems(zip, Global.Voices, Global.AppSettings.ResetVoicesMirror, Global.VoicesName, x => x.Name);

		/// <summary>Reset Embeddings</summary>
		public static void ResetEmbeddings(ZipStorer zip = null)
			=> ResetItems(zip, Global.Embeddings, Global.AppSettings.ResetEmbeddingsMirror, Global.EmbeddingsName, x => x.Name);

		/// <summary>Reset Lists</summary>
		public static void ResetLists(ZipStorer zip = null)
			=> ResetItems(zip, Global.Lists, Global.AppSettings.ResetListsMirror, Global.ListsName, x => x.Name);

		/// <summary>Reset UI Presets</summary>
		public static void ResetUiPresets(ZipStorer zip = null)
			=> ResetItems(zip, Global.UiPresets, Global.AppSettings.ResetUiPresetsMirror, Global.UiPresetsName, x => x.Name);

		#endregion

		#region Reset Services and Models

		/// <summary>Reset Services and Models</summary>
		public static void ResetServicesAndModels(ZipStorer zip = null)
		{
			bool closeZip;
			if (closeZip = zip == null)
				zip = GetSettingsZip();
			if (zip == null)
				return;
			// ---
			var zipAppDataItems = GetItemsFromZip(zip, Global.AppDataName, Global.AppData);
			var zipServices = zipAppDataItems[0].AiServices;
			var zipModels = zipAppDataItems[0].AiModels;
			// Remove Services and Models
			var zipServiceNames = zipServices.Select(t => t.Name.ToLower()).ToList();
			var servicesToRemove = Global.AppSettings.AiServices.Where(x => zipServiceNames.Contains(x.Name.ToLower())).ToArray();
			foreach (var service in servicesToRemove)
			{
				var modelsToRemove = Global.AppSettings.AiModels.Where(x => x.AiServiceId == service.Id).ToArray();
				foreach (var model in modelsToRemove)
					Global.AppSettings.AiModels.Remove(model);
				Global.AppSettings.AiServices.Remove(service);
			}
			// Add Services and Models
			foreach (var item in zipServices)
				Global.AppSettings.AiServices.Add(item);
			foreach (var item in zipModels)
				Global.AppSettings.AiModels.Add(item);
			// ---
			if (closeZip)
				zip.Close();
		}

		#endregion

		#region Lists

		public static string[] GetRequiredLists()
		{
			return new string[] {
				"Prompts"
			};
		}

		/// <summary>
		/// Reset XML items.
		/// </summary>
		private static void ResetItems<T>(ZipStorer zip, SettingsData<T> data, bool mirror, string name, Func<T, string> propertySelector)
		{
			bool closeZip;
			if (closeZip = zip == null)
				zip = GetSettingsZip();
			if (zip == null)
				return;
			// Update Lists
			var zipItems = GetItemsFromZip(zip, name, data);
			if (data.UseSeparateFiles)
			{
				var items = data.Items.ToArray();
				foreach (var item in items)
				{
					var error = data.DeleteItem(item as ISettingsFileItem);
					if (!string.IsNullOrEmpty(error))
						Global.ShowError(error);
				}
			}
			else
			{
				RemoveToReplace(data, zipItems, propertySelector);
			}
			data.PreventWriteToNewerFiles = false;
			data.Add(zipItems.ToArray());
			data.Save();
			data.PreventWriteToNewerFiles = true;
			// Extract other relevant non XML items from the zip.
			if (data.UseSeparateFiles)
			{
				var entries = zip.ReadCentralDir()
				.Where(x => x.FilenameInZip.StartsWith(name) && !x.FilenameInZip.EndsWith(".xml"))
				.ToArray();
				foreach (var entry in entries)
				{
					var path = Path.Combine(Global.AppData.XmlFile.Directory.FullName, entry.FilenameInZip);
					bool isDirectory = entry.FilenameInZip.EndsWith("/");
					if (isDirectory)
					{
						var di = new DirectoryInfo(path);
						if (!di.Exists)
							di.Create();
					}
					else
					{
						var bytes = AppHelper.ExtractFile(zip, entry.FilenameInZip);
						var fi = new FileInfo(path);
						if (!fi.Directory.Exists)
							fi.Directory.Create();
						if (File.Exists(path))
							File.Delete(path);
						File.WriteAllBytes(path, bytes);
					}
				}
			}
			// Close zip.
			if (closeZip)
				zip.Close();
		}

		public static int CheckRequiredItems(IList<EmbeddingsItem> items, ZipStorer zip = null)
		{
			return 0;
		}

		public static int CheckRequiredItems(IList<ListInfo> items, ZipStorer zip = null)
		{
			bool closeZip;
			if (closeZip = zip == null)
				zip = GetSettingsZip();
			if (zip == null)
				return 0;
			// ---
			var required = GetRequiredLists();
			var current = items.Select(x => x.Name).ToArray();
			var missing = required.Except(current).ToArray();
			// If all templates exist then return.
			if (missing.Length == 0)
				return 0;
			var zipItems = GetItemsFromZip(zip, Global.ListsName, Global.Lists, missing);
			foreach (var zipItem in zipItems)
				items.Add(zipItem);
			// ---
			if (closeZip)
				zip.Close();
			return missing.Length;
		}

		#endregion

		#region Reset Templates

		public const string TemplateGenerateTitleTaskName = "® System - Generate Title";
		public const string TemplateFormatMessageTaskName = "® System - Format Message";
		public const string TemplatePluginApprovalTaskName = "® System - Plugin Approval";
		public const string TempalteListsUpdateUserProfile = "Lists - Update User Profile";
		public const string TemplateAIChatPersonalized = "AI - Chat - Personalized";

		public const string TemplatePlugin_Model_TextToAudio = "® System - Text-To-Audio";
		public const string TemplatePlugin_Model_AudioToText = "® System - Audio-To-Text";
		public const string TemplatePlugin_Model_VideoToText = "® System - Video-To-Text";
		public const string TemplatePlugin_Model_TextToVideo = "® System - Text-To-Video";

		public static string[] GetRequiredTemplates()
		{
			return new string[] {
				TemplateGenerateTitleTaskName,
				TemplateFormatMessageTaskName,
				TemplatePluginApprovalTaskName,
				TempalteListsUpdateUserProfile,
				TemplateAIChatPersonalized,
			};
		}

		public static int CheckRequiredTemplates(IList<TemplateItem> items, ZipStorer zip = null)
		{
			bool closeZip;
			if (closeZip = zip == null)
				zip = GetSettingsZip();
			if (zip == null)
				return 0;
			// ---
			var required = GetRequiredTemplates();
			var current = items.Select(x => x.Name).ToArray();
			var missing = required.Except(current).ToArray();
			// If all templates exist then return.
			if (missing.Length == 0)
				return 0;
			var zipItems = GetItemsFromZip(zip, Global.TemplatesName, Global.Templates, missing);
			foreach (var zipItem in zipItems)
				items.Add(zipItem);
			// ---
			if (closeZip)
				zip.Close();
			return missing.Length;
		}

		/// <summary>
		/// Get items from the zip. entry pattern: filenameInZipStartsWith*.xml
		/// </summary>
		public static List<T> GetItemsFromZip<T>(ZipStorer zip, string filenameInZipStartsWith, SettingsData<T> data, params string[] names)
		{
			var list = new List<T>();
			var entries = zip.ReadCentralDir()
				.Where(x => x.FilenameInZip.StartsWith(filenameInZipStartsWith) && x.FilenameInZip.EndsWith(".xml"))
				.ToArray();
			foreach (var entry in entries)
			{
				// If names supplied then get only named templates.
				if (names?.Length > 0)
				{
					var nameInZip = Path.GetFileNameWithoutExtension(entry.FilenameInZip);
					if (!names.Contains(nameInZip))
						continue;
				}
				var bytes = AppHelper.ExtractFile(zip, entry.FilenameInZip);
				if (data.UseSeparateFiles)
				{
					var itemFile = data.DeserializeItem(bytes, false);
					list.Add(itemFile);
				}
				else
				{
					var dataFile = data.DeserializeData(bytes, false);
					list.AddRange(dataFile.Items);
				}
			}
			return list;
		}

		public static void ResetUI()
		{
			var w = AdjustForScreenshot(Global.AppSettings.ResetWindowWidth);
			var h = AdjustForScreenshot(Global.AppSettings.ResetWindowHeight);
			if (Global.AppSettings.ResetWindowWidth != w)
				Global.AppSettings.ResetWindowWidth = w;
			if (Global.AppSettings.ResetWindowHeight != h)
				Global.AppSettings.ResetWindowHeight = h;
			var items = Global.AppSettings.PanelSettingsList.ToArray();
			foreach (var item in items)
				ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(item);
			var ps = Global.AppSettings.StartPosition;
			if (!Global.IsVsExtension)
			{
				var window = ControlsHelper.GetParent<System.Windows.Window>(Global.MainControl);
				//var pixRect = PositionSettings.GetPixelsBoundaryRectangle(this);
				//var pixRectWin = PositionSettings.GetPixelsBoundaryRectangle(window);
				var width = Math.Max(w, window.MinWidth);
				var height = Math.Max(h, window.MinHeight);
				var content = (System.Windows.FrameworkElement)window.Content;
				// Get space taken by the window borders.
				var wSpace = window.ActualWidth - content.ActualWidth;
				var hSpace = window.ActualHeight - content.ActualHeight;
				//var tPad = pixRect.Top - pixRectWin.Top;
				//var lPad = pixRect.Left - pixRectWin.Left;
				//var padPoint = new Point(tPad, lPad);
				var size = new System.Windows.Size(width + wSpace, height + hSpace);
				var point = new System.Windows.Point(window.Left, window.Top);
				var newSize = PositionSettings.ConvertToDiu(size);
				var newPoint = PositionSettings.ConvertToDiu(point);
				//var newPadPoint = PositionSettings.ConvertToDiu(padPoint);
				ps.Left = (int)(newPoint.X / 2 / 3 / 5) * 2 * 3 * 5;
				ps.Top = (int)(newPoint.Y / 2 / 3 / 5) * 2 * 3 * 5;
				ps.Width = newSize.Width;
				ps.Height = newSize.Height;
				ps.LoadPosition(window);
			}
		}


		/// <summary>
		/// Adjusts the provided dimension to the nearest perfect size for screenshots, 
		/// meeting the criteria of divisibility by 2, 3, 4, and 10.
		/// </summary>
		/// <param name="value">The original size of the screenshot dimension 
		/// (width or height) to be adjusted.</param>
		/// <returns>The adjusted size, meeting the criteria of being a multiple of 2, 3, 4, and 10 
		/// for optimal resizing quality.</returns>
		private static int AdjustForScreenshot(int value)
		{
			// The LCM of 2, 3, 4, and 10 to ensure scaling and quality criteria
			const int perfectDivisor = 60;
			// If the value already meets the perfect criteria then return.
			if (value % perfectDivisor == 0)
				return value;
			// Calculate the nearest higher multiple of 60
			int adjustedValue = ((value / perfectDivisor) + 1) * perfectDivisor;
			return adjustedValue;
		}

		#endregion

		#region General Methods

		private static void RemoveToReplace<T>(SettingsData<T> data, IList<T> items, Func<T, string> propertySelector)
		{
			// Remove lists which will be replaced.
			var names = items.Select(t => propertySelector(t).ToLower()).ToList();
			var itemsToRemove = data.Items.Where(x => names.Contains(propertySelector(x).ToLower())).ToArray();
			data.Remove(itemsToRemove);
		}

		public static ZipStorer GetSettingsZip()
		{
			ZipStorer zip = null;
			// Check if settings zip file with the same name as the executable exists.
			var settingsFile = $"{AssemblyInfo.Entry.ModuleBasePath}.Settings.zip";
			// ------------------------------------------------
			// Parse command line arguments and override default settings file location.
			var args = Environment.GetCommandLineArgs();
			var ic = new JocysCom.ClassLibrary.Configuration.Arguments(args);
			if (ic.ContainsKey("SettingsFile"))
			{
				var argValue = ic["SettingsFile"];
				if (!string.IsNullOrEmpty(argValue))
				{
					if (File.Exists(argValue))
						settingsFile = argValue;
				}
			}
			// ------------------------------------------------
			// Load zip file.
			if (File.Exists(settingsFile))
			{
				// Use external file.
				zip = ZipStorer.Open(settingsFile, FileAccess.Read);
			}
			else if (Global.AppSettings.IsEnterprise)
			{
				// Use external URL or local file specified by the user.
				var path = JocysCom.ClassLibrary.Configuration.AssemblyInfo.ExpandPath(Global.AppSettings.ConfigurationUrl);
				var isUrl = Uri.TryCreate(path, UriKind.Absolute, out Uri uri) && uri.Scheme != Uri.UriSchemeFile;
				if (isUrl)
					zip = GetZipFromUrl(path);
				else if (File.Exists(path))
					zip = ZipStorer.Open(path, FileAccess.Read);
			}
			else
			{
				// Use embedded resource.
				zip = AppHelper.GetZip("Resources.Settings.zip", typeof(Global).Assembly);
			}
			if (zip == null)
				return null;
			return zip;
		}

		public static ZipStorer GetZipFromUrl(string url)
		{
			var docItem = Helper.RunSynchronously(async () => await Web.DownloadContentAuthenticated(url));
			if (!string.IsNullOrEmpty(docItem.Error))
			{
				Global.ShowError($"{nameof(GetZipFromUrl)} error: {docItem.Error}");
				return null;
			}
			var zipBytes = docItem.GetDataBinary();
			var ms = new MemoryStream(zipBytes);
			try
			{
				// Open an existing zip file for reading.
				var zip = ZipStorer.Open(ms, FileAccess.Read);
				return zip;
			}
			catch (Exception ex)
			{
				Global.ShowError($"{nameof(GetZipFromUrl)} error: {ex.Message}");
				return null;
			}
		}

		#endregion


	}
}
