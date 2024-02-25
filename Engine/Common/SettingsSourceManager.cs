using JocysCom.ClassLibrary;
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

		public static List<TemplateItem> GetDefaultTemplates()
		{
			List<TemplateItem> items = null;
			try
			{
				items = _GetDefaultTemplates();
			}
			catch (Exception ex)
			{
				Global.ShowError("GetDefaultTemplates() errro: " + ex.Message);
			}
			return items ?? new List<TemplateItem>();
		}

		public static List<TemplateItem> _GetDefaultTemplates()
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
			var items = GetTemplatesFromZip(zip);
			zip.Close();
			return items;
		}

		public static ZipStorer GetZipFromUrl(string url)
		{
			var docItem = Helper.RunSynchronously(async () => await Basic.DownloadContents(url));
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

		private static List<TemplateItem> GetTemplatesFromZip(ZipStorer zip)
		{
			var data = Global.Templates;
			var list = new List<TemplateItem>();
			var entries = zip.ReadCentralDir()
				.Where(x => x.FilenameInZip.StartsWith(Global.TemplatesName))
				.ToArray();
			foreach (var entry in entries)
			{
				var bytes = AppHelper.ExtractFile(zip, entry.FilenameInZip);
				var item = data.DeserializeItem(bytes, false);
				list.Add(item);
			}
			return list;
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
			var items = GetTemplatesFromZip(zip);
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


	}
}
