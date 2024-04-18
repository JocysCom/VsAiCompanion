using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Configuration;
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

		public static void ResetSettings()
		{
			try
			{
				var zip = GetSettingsZip();
				// Load data from a single XML file.
				var zipAppData = GetDataFromZip(zip, Global.AppData.XmlFile.Name, Global.AppData);
				// Load data from multiple XML files.
				var zipTasks = GetItemsFromZip(zip, Global.TasksName, Global.Tasks);
				var zipTemplates = GetItemsFromZip(zip, Global.TemplatesName, Global.Templates);
				var zipLists = GetItemsFromZip(zip, Global.ListsName, Global.Lists);
				var zipEmbeddings = GetItemsFromZip(zip, Global.EmbeddingsName, Global.Embeddings);
				var zipServices = zipAppData.Items[0].AiServices;
				var zipModels = zipAppData.Items[0].AiModels;
				var zipAppSettings = zipAppData.Items[0];
				Global.Templates.PreventWriteToNewerFiles = false;
				Global.Tasks.PreventWriteToNewerFiles = false;
				Global.Lists.PreventWriteToNewerFiles = false;
				Global.AppData.PreventWriteToNewerFiles = false;
				RemoveToReplace(Global.Tasks, zipTasks);
				RemoveToReplace(Global.Templates, zipTemplates);
				RemoveToReplace(Global.Lists, zipLists);
				RemoveToReplace(Global.Embeddings, zipEmbeddings);
				// Remove AiServices.
				var zipServiceNames = zipServices.Select(t => t.Name.ToLower()).ToList();
				var servicesToRemove = Global.AppSettings.AiServices.Where(x => zipServiceNames.Contains(x.Name.ToLower())).ToArray();
				foreach (var service in servicesToRemove)
				{
					var modelsToRemove = Global.AppSettings.AiModels.Where(x => x.AiServiceId == service.Id).ToArray();
					foreach (var model in modelsToRemove)
						Global.AppSettings.AiModels.Remove(model);
					Global.AppSettings.AiServices.Remove(service);
				}
				ResetPrompts(zip);
				// Add user settings.
				foreach (var item in zipAppData.Items[0].AiServices)
					Global.AppSettings.AiServices.Add(item);
				foreach (var item in zipAppData.Items[0].AiModels)
					Global.AppSettings.AiModels.Add(item);
				Global.Embeddings.Add(zipEmbeddings.ToArray());
				Global.Lists.Add(zipLists.ToArray());
				Global.Templates.Add(zipTemplates.ToArray());
				Global.Tasks.Add(zipTasks.ToArray());
				// Copy other settings.
				RuntimeHelper.CopyProperties(zipAppSettings, Global.AppSettings, true);
				RuntimeHelper.CopyProperties(zipAppSettings.TaskData, Global.AppSettings.TaskData, true);
				RuntimeHelper.CopyProperties(zipAppSettings.TemplateData, Global.AppSettings.TemplateData, true);
				RuntimeHelper.CopyProperties(zipAppSettings.FineTuningData, Global.AppSettings.FineTuningData, true);
				RuntimeHelper.CopyProperties(zipAppSettings.AiServiceData, Global.AppSettings.AiServiceData, true);
				// Save settings.
				Global.SaveSettings();
				Global.Lists.PreventWriteToNewerFiles = true;
				Global.Embeddings.PreventWriteToNewerFiles = true;
				Global.Templates.PreventWriteToNewerFiles = true;
				Global.Tasks.PreventWriteToNewerFiles = true;
				Global.AppData.PreventWriteToNewerFiles = true;
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

		public static Guid OpenAiServiceId
			=> AppHelper.GetGuid(nameof(AiService), OpenAiName);
		// Must be string constant or OpenAiServiceId property will get empty string.

		public const string OpenAiName = "Open AI";

		#region Reset App Settings

		/// <summary>
		/// Does not reset AiServices and AiModels.
		/// </summary>
		public static void ResetAppSettings()
		{
			var settings = Global.AppSettings;
			// Reset all app settings except list of services and list of models.
			var exclude = new string[] { nameof(settings.AiServices), nameof(settings.AiModels) };
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(settings, false, exclude);
		}


		/// <summary>Reset Prompts</summary>
		public static void ResetPrompts(ZipStorer zip = null)
		{
			bool closeZip;
			if (closeZip = zip == null)
				zip = GetSettingsZip();
			// Update Prompts.
			var zipPromptItems = GetDataFromZip(zip, Global.PromptItems.XmlFile.Name, Global.PromptItems);
			var zipPromptNames = zipPromptItems.Items.Select(t => t.Name.ToLower()).ToList();
			var promptsToRemove = Global.PromptItems.Items.Where(x => zipPromptNames.Contains(x.Name.ToLower())).ToArray();
			Global.PromptItems.Remove(promptsToRemove);
			Global.PromptItems.Add(zipPromptItems.Items.ToArray());
			// Close zip.
			if (closeZip)
				zip.Close();
		}

		#endregion

		#region Lists

		public static string[] GetRequiredLists()
		{
			return new string[] {
				"API - Demo",
			};
		}


		/// <summary>Reset Lists</summary>
		public static void ResetLists(ZipStorer zip = null)
			=> ResetItems(zip, Global.Lists, Global.ListsName);

		/// <summary>Reset Lists</summary>
		public static void ResetEmbeddings(ZipStorer zip = null)
		{
			bool closeZip;
			if (closeZip = zip == null)
				zip = GetSettingsZip();
			ResetItems(zip, Global.Embeddings, Global.EmbeddingsName);
			ResetOtherItems(zip, Global.Embeddings, Global.EmbeddingsName);
			// Close zip.
			if (closeZip)
				zip.Close();
		}

		private static void ResetOtherItems<T>(ZipStorer zip, SettingsData<T> data, string name) where T : SettingsFileItem
		{
			var entries = zip.ReadCentralDir()
				.Where(x => x.FilenameInZip.StartsWith(name) && !x.FilenameInZip.EndsWith(".xml"))
				.ToArray();
			foreach (var entry in entries)
			{
				var bytes = AppHelper.ExtractFile(zip, entry.FilenameInZip);
				var path = Path.Combine(Global.AppData.XmlFile.Directory.FullName, entry.FilenameInZip);
				var fi = new FileInfo(path);
				if (!fi.Directory.Exists)
					fi.Directory.Create();
				if (File.Exists(path))
					File.Delete(path);
				System.IO.File.WriteAllBytes(path, bytes);
			}
		}

		private static void ResetItems<T>(ZipStorer zip, SettingsData<T> data, string name) where T : SettingsFileItem
		{
			bool closeZip;
			if (closeZip = zip == null)
				zip = GetSettingsZip();
			// Update Lists
			var zipItems = GetItemsFromZip(zip, name, data);
			RemoveToReplace(data, zipItems);
			data.Add(zipItems.ToArray());
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

		public static void ResetTemplates(ZipStorer zip = null)
		{
			bool closeZip;
			if (closeZip = zip == null)
				zip = GetSettingsZip();
			// ---
			var data = Global.Templates;
			var zipTemplates = GetItemsFromZip(zip, Global.TemplatesName, Global.Templates);
			if (zipTemplates.Count == 0)
				return;
			var items = data.Items.ToArray();
			foreach (var item in items)
			{
				var error = data.DeleteItem(item);
				if (!string.IsNullOrEmpty(error))
					Global.ShowError(error);
			}
			data.PreventWriteToNewerFiles = false;
			foreach (var item in zipTemplates)
			{
				data.Items.Add(item);
			}
			// Templates.Load();
			data.Save();
			data.PreventWriteToNewerFiles = true;
			// ---
			if (closeZip)
				zip.Close();
		}

		public static List<T> GetItemsFromZip<T>(ZipStorer zip, string name, SettingsData<T> data, params string[] names)
		{
			var list = new List<T>();
			var entries = zip.ReadCentralDir()
				.Where(x => x.FilenameInZip.StartsWith(name) && x.FilenameInZip.EndsWith(".xml"))
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
				var item = data.DeserializeItem(bytes, false);
				list.Add(item);
			}
			return list;
		}

		private static SettingsData<T> GetDataFromZip<T>(ZipStorer zip, string name, SettingsData<T> data)
		{
			var list = new List<T>();
			var entry = zip.ReadCentralDir()
				.Where(x => x.FilenameInZip.StartsWith(name))
				.FirstOrDefault();
			var bytes = AppHelper.ExtractFile(zip, entry.FilenameInZip);
			var item = data.DeserializeData(bytes, false);
			return item;
		}


		public static List<TemplateItem> GetTempaltesFromEmbeddedResourceZip()
		{
			var asm = typeof(Global).Assembly;
			var zip = AppHelper.GetZip("Resources.Settings.zip", asm);
			if (zip == null)
			{
				Global.ShowError("Resource 'Resources.Settings.zip' not found!");
				return new List<TemplateItem>();
			}
			var items = GetItemsFromZip(zip, Global.TemplatesName, Global.Templates);
			zip.Close();
			return items;
		}

		public static List<TemplateItem> GetTempaltesFromEmbeddedResources()
		{
			var data = Global.Templates;
			var list = new List<TemplateItem>();
			var asm = typeof(Global).Assembly;
			var keys = asm.GetManifestResourceNames()
				.Where(x => x.Contains("Resources.Settings.Templates"))
				.ToList();
			foreach (var key in keys)
			{
				var bytes = Helper.GetResource<byte[]>(key, asm);
				var item = data.DeserializeItem(bytes, false);
				list.Add(item);
			}
			return list;
		}

		#endregion

		#region General Methods

		private static void RemoveToReplace<T>(SettingsData<T> data, IList<T> items) where T : SettingsFileItem
		{
			// Remove lists which will be replaced.
			var names = items.Select(t => t.Name.ToLower()).ToList();
			var itemsToRemove = data.Items.Where(x => names.Contains(x.Name.ToLower())).ToArray();
			data.Remove(itemsToRemove);
		}

		public static ZipStorer GetSettingsZip()
		{
			ZipStorer zip = null;
			if (Global.AppSettings.IsEnterprise)
			{
				var path = JocysCom.ClassLibrary.Configuration.AssemblyInfo.ExpandPath(Global.AppSettings.ConfigurationUrl);
				var isUrl = Uri.TryCreate(path, UriKind.Absolute, out Uri uri) && uri.Scheme != Uri.UriSchemeFile;
				zip = isUrl
					? GetZipFromUrl(path)
					: ZipStorer.Open(path, FileAccess.Read);
			}
			else
			{
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
