﻿using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Configuration;
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
				var zipAppData = GetDataFromZip(zip, Global.AppData.XmlFile.Name, Global.AppData);
				var zipTasks = GetItemsFromZip(zip, Global.TasksName, Global.Tasks);
				var zipTemplates = GetItemsFromZip(zip, Global.TemplatesName, Global.Templates);
				var zipServices = zipAppData.Items[0].AiServices;
				var zipModels = zipAppData.Items[0].AiModels;
				Global.Templates.PreventWriteToNewerFiles = false;
				Global.Tasks.PreventWriteToNewerFiles = false;
				Global.AppData.PreventWriteToNewerFiles = false;
				// Remove tasks which will be replaced.
				var zipTaskNames = zipTasks.Select(t => t.Name.ToLower()).ToList();
				var tasksToRemove = Global.Tasks.Items.Where(x => zipTaskNames.Contains(x.Name.ToLower())).ToArray();
				Global.Tasks.Remove(tasksToRemove);
				// Remove templates which will be replaced.
				var zipTemplateNames = zipTemplates.Select(t => t.Name.ToLower()).ToList();
				var templatesToRemove = Global.Templates.Items.Where(x => zipTemplateNames.Contains(x.Name.ToLower())).ToArray();
				Global.Templates.Remove(templatesToRemove);
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
				// Add user settings.
				foreach (var item in zipAppData.Items[0].AiServices)
					Global.AppSettings.AiServices.Add(item);
				foreach (var item in zipAppData.Items[0].AiModels)
					Global.AppSettings.AiModels.Add(item);
				Global.Templates.Add(zipTemplates.ToArray());
				Global.Tasks.Add(zipTasks.ToArray());
				Global.SaveSettings();
				Global.Templates.PreventWriteToNewerFiles = true;
				Global.Tasks.PreventWriteToNewerFiles = true;
				Global.AppData.PreventWriteToNewerFiles = true;
				Global.RaiseOnAiServicesUpdated();
				Global.RaiseOnAiModelsUpdated();
				Global.RaiseOnTasksUpdated();
				Global.RaiseOnTemplatesUpdated();
			}
			catch (Exception ex)
			{
				Global.ShowError("ResetSettings() error: " + ex.Message);
			}
		}

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

		#endregion

		#region Reset Templates

		public static void ResetTemplates()
		{
			var zip = GetSettingsZip();
			ResetTemplates(zip);
			zip.Close();
		}

		public static void ResetTemplates(ZipStorer zip)
		{
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