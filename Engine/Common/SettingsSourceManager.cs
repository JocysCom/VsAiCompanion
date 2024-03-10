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
				var zipServices = zipAppData.Items[0].AiServices;
				var zipModels = zipAppData.Items[0].AiModels;
				var zipAppSettings = zipAppData.Items[0];
				Global.Templates.PreventWriteToNewerFiles = false;
				Global.Tasks.PreventWriteToNewerFiles = false;
				Global.Lists.PreventWriteToNewerFiles = false;
				Global.AppData.PreventWriteToNewerFiles = false;
				// Remove tasks which will be replaced.
				var zipTaskNames = zipTasks.Select(t => t.Name.ToLower()).ToList();
				var tasksToRemove = Global.Tasks.Items.Where(x => zipTaskNames.Contains(x.Name.ToLower())).ToArray();
				Global.Tasks.Remove(tasksToRemove);
				// Remove templates which will be replaced.
				var zipTemplateNames = zipTemplates.Select(t => t.Name.ToLower()).ToList();
				var templatesToRemove = Global.Templates.Items.Where(x => zipTemplateNames.Contains(x.Name.ToLower())).ToArray();
				Global.Templates.Remove(templatesToRemove);
				// Remove Lists which will be replaced.
				var zipListsNames = zipLists.Select(t => t.Name.ToLower()).ToList();
				var listsToRemove = Global.Lists.Items.Where(x => zipListsNames.Contains(x.Name.ToLower())).ToArray();
				Global.Lists.Remove(listsToRemove);
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

		/// <summary>Reset Prompts</summary>
		public static void ResetLists(ZipStorer zip = null)
		{
			bool closeZip;
			if (closeZip = zip == null)
				zip = GetSettingsZip();
			// Update Lists
			var zipLists = GetItemsFromZip(zip, Global.ListsName, Global.Lists);
			// Remove lists which will be replaced.
			var zipListsNames = zipLists.Select(t => t.Name.ToLower()).ToList();
			var listsToRemove = Global.Lists.Items.Where(x => zipListsNames.Contains(x.Name.ToLower())).ToArray();
			Global.Lists.Remove(listsToRemove);
			Global.Lists.Add(zipLists.ToArray());
			// Close zip.
			if (closeZip)
				zip.Close();
		}

		#endregion

		#region Reset Templates

		public static void ResetTemplates(ZipStorer zip = null)
		{
			bool closeZip;
			if (closeZip = zip == null)
				zip = GetSettingsZip();

			var templates = Global.Templates;
			var defaultItems = GetItemsFromZip(zip, Global.TemplatesName, Global.Templates);
			if (defaultItems.Count == 0)
				return;
			var items = templates.Items.ToArray();
			foreach (var item in items)
			{
				var error = templates.DeleteItem(item);
				if (!string.IsNullOrEmpty(error))
					Global.ShowError(error);
			}
			templates.PreventWriteToNewerFiles = false;
			foreach (var item in defaultItems)
			{
				templates.Items.Add(item);
			}
			// Templates.Load();
			templates.Save();
			templates.PreventWriteToNewerFiles = true;

			if (closeZip)
				zip.Close();
		}

		public static List<T> GetItemsFromZip<T>(ZipStorer zip, string name, SettingsData<T> data)
		{
			var list = new List<T>();
			var entries = zip.ReadCentralDir()
				.Where(x => x.FilenameInZip.StartsWith(name) && x.FilenameInZip.EndsWith(".xml"))
				.ToArray();
			foreach (var entry in entries)
			{
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

		public static ZipStorer GetSettingsZip()
		{
			ZipStorer zip = null;
			if (Global.AppSettings.IsEnterprise)
			{
				var path = Global.AppSettings.ConfigurationUrl;
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
			var docItem = Helper.RunSynchronously(async () => await Basic.DownloadContentAuthenticated(url));
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
