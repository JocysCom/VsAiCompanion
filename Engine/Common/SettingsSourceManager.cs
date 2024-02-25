using JocysCom.ClassLibrary;
using System.Collections.Generic;
using System.Linq;

namespace JocysCom.VS.AiCompanion.Engine
{
	public static class SettingsSourceManager
	{

		public static List<TemplateItem> GetDefaultTemplates()
		{
			if (Global.AppSettings.IsEnterprise)
			{
				return GetTempaltesFromEmbeddedResources();
			}
			else
			{
				return GetTempaltesFromEmbeddedResourceZip();
				//return GetTempaltesFromEmbeddedResources();
			}
		}

		public static List<TemplateItem> GetTempaltesFromEmbeddedResourceZip()
		{
			var data = Global.Templates;
			var list = new List<TemplateItem>();
			var asm = typeof(Global).Assembly;
			var zip = AppHelper.GetZip("Resources.Settings.zip", asm);
			if (zip == null)
			{
				Global.ShowError("Resource 'Resources.Settings.zip' not found!");
				return list;
			}
			var entries = zip.ReadCentralDir()
				.Where(x => x.FilenameInZip.StartsWith(Global.TemplatesName))
				.ToArray();
			foreach (var entry in entries)
			{
				var bytes = AppHelper.ExtractFile(zip, entry.FilenameInZip);
				var item = data.DeserializeItem(bytes, false);
				list.Add(item);
			}
			zip.Close();
			return list;
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
