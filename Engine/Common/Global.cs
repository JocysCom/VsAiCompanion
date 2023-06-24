using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace JocysCom.VS.AiCompanion.Engine
{
	public static class Global
	{
		// Get or Set multiple documents.
		public static Func<List<DocItem>> GetSolution = () => new List<DocItem>();
		public static Action<List<DocItem>> SetSolution = (x) => { };
		public static Func<List<DocItem>> GetActiveProject = () => new List<DocItem>();
		public static Action<List<DocItem>> SetActiveProject = (x) => { };
		public static Func<List<DocItem>> GetSelectedProject = () => new List<DocItem>();
		public static Action<List<DocItem>> SetSelectedProject = (x) => { };
		public static Func<List<DocItem>> GetSelectedDocuments = () => new List<DocItem>();
		public static Action<List<DocItem>> SetSelectedDocuments = (x) => { };
		// Get or Set single document.
		public static Func<DocItem> GetActiveDocument = () => new DocItem();
		public static Action<string> SetActiveDocument = (x) => { };
		public static Func<DocItem> GetSelection = () => new DocItem();
		public static Action<string> SetSelection = (x) => { };
		public static Func<DocItem> GetClipboard = () => new DocItem();
		public static Action<string> SetClipboard = (x) => { };
		// Errors.
		public static Func<ErrorItem> GetSelectedError = () => new ErrorItem();
		// Format code.
		public static Action EditFormatDocument = () => { };
		public static Action EditFormatSelection = () => { };
		// Get solution properties (macros).
		public static Func<List<PropertyItem>> GetEnvironmentProperties = () => new List<PropertyItem>();
		public static Func<List<PropertyItem>> GetReservedProperties = () => new List<PropertyItem>();
		public static Func<List<PropertyItem>> GetOtherProperties = () => new List<PropertyItem>();

		public static AssemblyInfo Info { get; } = new AssemblyInfo(typeof(Global).Assembly);

		public static AppData AppSettings =>
			AppData.Items.FirstOrDefault();

		public static SettingsData<AppData> AppData =
			new SettingsData<AppData>(null, true, null, System.Reflection.Assembly.GetExecutingAssembly());

		public static SettingsData<TemplateItem> Templates =
			new SettingsData<TemplateItem>($"{nameof(Templates)}.xml", true, null, System.Reflection.Assembly.GetExecutingAssembly())
			{
				UseSeparateFiles = true,
				FileNamePropertyName = nameof(TemplateItem.Name),
			};

		public static SettingsData<TemplateItem> Tasks =
		new SettingsData<TemplateItem>($"{nameof(Tasks)}.xml", true, null, System.Reflection.Assembly.GetExecutingAssembly())
		{
			UseSeparateFiles = true,
			FileNamePropertyName = nameof(TemplateItem.Name),
		};

		public static SortableBindingList<TemplateItem> GetItems(ItemType type)
		{
			switch (type)
			{
				case ItemType.Task: return Tasks.Items;
				case ItemType.Template: return Templates.Items;
				default: return new SortableBindingList<TemplateItem>();
			}
		}

		public static ISettingsData GetSettings(ItemType type)
		{
			switch (type)
			{
				case ItemType.Task: return Tasks;
				case ItemType.Template: return Templates;
				default: return new SettingsData<TemplateItem>();
			}
		}

		public static bool IsIncompleteSettings()
		{
			var itemsRequired = new List<string>();
			if (string.IsNullOrEmpty(AppSettings.OpenAiSettings.ApiSecretKey))
				itemsRequired.Add("API Key");
			if (string.IsNullOrEmpty(AppSettings.OpenAiSettings.ApiOrganizationId))
				itemsRequired.Add("API Organization ID");
			if (itemsRequired.Count > 0)
			{
				MainControl.MainTabControl.SelectedItem = MainControl.OptionsTabItem;
				var s = string.Join(" and ", itemsRequired);
				MainControl.InfoPanel.SetWithTimeout(MessageBoxImage.Warning, $"Please provide the {s}.");
			}
			return itemsRequired.Count > 0;
		}

		public static event EventHandler OnSaveSettings;

		public static void SaveSettings()
		{

			OnSaveSettings?.Invoke(null, new EventArgs());
			AppData.Save();
			Templates.Save();
			Tasks.Save();
		}

		public static void InitDefaultSettings()
		{
			if (AppData.Items.Count == 0)
			{
				AppData.Items.Add(new AppData());
				AppData.Save();
			}
			Companions.ChatGPT.Client.Settings = Global.AppSettings.OpenAiSettings;
		}

		public static void LoadSettings()
		{
			AppData.Load();
			Templates.OnValidateData += Templates_OnValidateData;
			Templates.Load();
			if (DefaultTemplatesAdded)
				Templates.Save();
			Tasks.OnValidateData += Tasks_OnValidateData;
			Tasks.Load();
			if (DefaultTasksAdded)
				Tasks.Save();
			Templates.SetFileMonitoring(true);
			Tasks.SetFileMonitoring(true);
			// If old settings version then reset templates.
			if (AppData.Version < 2) {
				AppData.Version = 2;
				ResetTemplates();
			}
		}

		public static void ResetTemplates()
		{
			var items = Templates.Items.ToArray();
			foreach (var item in items)
				Templates.DeleteItem(item);
			Templates.Load();
			Templates.Save();
		}

		public static void ResetAppSettings()
		{
			var exclude = new string[] { nameof(AppSettings.OpenAiSettings) };
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(AppSettings, false, exclude);

		}

		private static bool DefaultTemplatesAdded = false;
		private static bool DefaultTasksAdded = false;

		private static void Templates_OnValidateData(object sender, SettingsData<TemplateItem>.SettingsDataEventArgs e)
		{
			var sd = (SettingsData<TemplateItem>)sender;
			if (e.Items.Count > 0)
				return;
			var asm = typeof(Global).Assembly;
			var keys = asm.GetManifestResourceNames()
				.Where(x => x.Contains("Resources.Templates"))
				.ToList();
			foreach (var key in keys)
			{
				var bytes = Helper.GetResource<byte[]>(key, asm);
				var item = sd.DeserializeItem(bytes, false);
				e.Items.Add(item);
			}
			DefaultTemplatesAdded = true;
		}

		private static void Tasks_OnValidateData(object sender, SettingsData<TemplateItem>.SettingsDataEventArgs e)
		{
			var sd = (SettingsData<TemplateItem>)sender;
			if (e.Items.Count > 0)
				return;
			var asm = typeof(Global).Assembly;
			var keys = asm.GetManifestResourceNames()
				.Where(x => x.Contains("Resources.Templates"))
				.ToList();
			foreach (var key in keys)
			{
				// Add only templates which mention chat.
				if (key.IndexOf("Chat", StringComparison.OrdinalIgnoreCase) == -1)
					continue;
				var bytes = Helper.GetResource<byte[]>(key, asm);
				var item = sd.DeserializeItem(bytes, false);
				e.Items.Add(item.Copy(true));
			}
			DefaultTemplatesAdded = true;
			DefaultTasksAdded = true;
		}

		public static void ClearItems()
		{
			Templates.Items.Clear();
			Tasks.Items.Clear();
		}

		public static MainControl MainControl;

		public static bool IsVsExtesion { get; set; }
		public static string VsExtensionFeatureMessage = "This feature is only available when the application is run as an extension in Visual Studio.";

	}
}
