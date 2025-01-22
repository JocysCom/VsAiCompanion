using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Collections;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Controls.HotKey;
using JocysCom.ClassLibrary.Controls.Themes;
using JocysCom.VS.AiCompanion.DataClient;
using JocysCom.VS.AiCompanion.Engine.Controls;
using JocysCom.VS.AiCompanion.Engine.Controls.Shared;
using JocysCom.VS.AiCompanion.Engine.Security;
using JocysCom.VS.AiCompanion.Engine.Speech;
using JocysCom.VS.AiCompanion.Plugins.Core;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JocysCom.VS.AiCompanion.Engine
{
	public static partial class Global
	{

		public static ISolutionHelper _SolutionHelper;
		public static Func<CancellationToken, Task> SwitchToVisualStudioThreadAsync = (CancellationToken cancellationToken) => { return null; };

		// Get or Set multiple documents.
		public static Action<List<DocItem>> SetSolution = (x) => { };
		public static Func<List<DocItem>> GetActiveProject = () => new List<DocItem>();
		public static Action<List<DocItem>> SetActiveProject = (x) => { };
		public static Action<List<DocItem>> SetSelectedProject = (x) => { };
		public static Func<List<DocItem>> GetSelectedDocuments = () => new List<DocItem>();
		public static Action<List<DocItem>> SetSelectedDocuments = (x) => { };
		public static Func<List<DocItem>> GetOpenDocuments = () => new List<DocItem>();
		public static Action<List<DocItem>> SetOpenDocuments = (x) => { };
		// Get or Set single document.
		public static Func<DocItem> GetClipboard = () => new DocItem();
		public static Action<string> SetClipboard = (x) => { };
		// Get solution properties (macros).
		public static Func<List<PropertyItem>> GetEnvironmentProperties = () => new List<PropertyItem>();
		public static Func<List<PropertyItem>> GetReservedProperties = () => new List<PropertyItem>();
		public static Func<List<PropertyItem>> GetOtherProperties = () => new List<PropertyItem>();

		public static HotKeyHelper AiWindowHotKeyHelper;

		/// <summary>
		/// Load hotkeys when app settins loaded.
		/// </summary>
		/// <param name="window"></param>
		public static void InitHotKeys(Window window)
		{
			AiWindowHotKeyHelper = new HotKeyHelper(window);
			AiWindowHotKeyHelper.HotKeyPressed += AiWindowHotKeyHelper_HotKeyPressed;
			UpdateAiWindowHotKey();
		}

		static void UpdateAiWindowHotKey()
		{
			var hk = AiWindowHotKeyHelper;
			if (hk == null || hk.IsSuspended)
				return;
			var isEnabled = AppSettings.AiWindowHotKeyEnabled;
			if (isEnabled)
				hk.RegisterHotKey(AppSettings.AiWindowHotKey);
			else
				hk.UnregisterHotKey();
		}

		private static void AiWindowHotKeyHelper_HotKeyPressed(object sender, EventArgs e)
		{
			AiWindow.ShowUnderTheMouse();
		}

		public static AssemblyInfo Info { get; } = new AssemblyInfo(typeof(Global).Assembly);

		public static TrayManager TrayManager { get; set; }

		public static AppData AppSettings
			=> AppData.Items.FirstOrDefault();

		/// <summary>
		/// Get user profile with the access token.
		/// </summary>
		public static UserProfile UserProfile
			=> AppSettings.UserProfiles.First();

		public const string AppDataName = nameof(AppData);


		public static SettingsData<AppData> AppData =
			new SettingsData<AppData>($"{AppDataName}.xml", true, null, System.Reflection.Assembly.GetExecutingAssembly());

		public const string PromptsName = nameof(Prompts);

		public static SettingsData<PromptItem> Prompts =
			new SettingsData<PromptItem>($"{PromptsName}.xml", true, null, System.Reflection.Assembly.GetExecutingAssembly());

		public const string VoicesName = nameof(Voices);

		public static SettingsData<VoiceItem> Voices =
			new SettingsData<VoiceItem>($"{VoicesName}.xml", true, null, System.Reflection.Assembly.GetExecutingAssembly());

		public const string ListsName = nameof(Lists);

		public static SettingsData<ListInfo> Lists =
			new SettingsData<ListInfo>($"{ListsName}.xml", true, null, System.Reflection.Assembly.GetExecutingAssembly())
			{
				UseSeparateFiles = true,
			};

		public const string ResetsName = nameof(Resets);

		public static SettingsData<ListInfo> Resets =
			new SettingsData<ListInfo>($"{ResetsName}.xml", true, null, System.Reflection.Assembly.GetExecutingAssembly());

		public const string UiPresetsName = nameof(UiPresets);

		public static SettingsData<UiPresetItem> UiPresets =
			new SettingsData<UiPresetItem>($"{UiPresetsName}.xml", true, null, System.Reflection.Assembly.GetExecutingAssembly())
			{
				UseSeparateFiles = true,
			};

		// Preparnig to move AI Services and AI Models to a separate settings file.
		public static bool StoreAiServicesAndModelsInSeparateFile = false;

		public const string AiServicesName = nameof(AiServices);
		public static SettingsData<AiService> AiServices =
			new SettingsData<AiService>($"{AiServicesName}.xml", true, null, System.Reflection.Assembly.GetExecutingAssembly());

		public const string AiModelsName = nameof(AiModels);
		public static SettingsData<AiModel> AiModels =
			new SettingsData<AiModel>($"{AiModelsName}.xml", true, null, System.Reflection.Assembly.GetExecutingAssembly());


		public const string EmbeddingsName = nameof(Embeddings);
		public static SettingsData<EmbeddingsItem> Embeddings =
			new SettingsData<EmbeddingsItem>($"{EmbeddingsName}.xml", true, null, System.Reflection.Assembly.GetExecutingAssembly())
			{
				UseSeparateFiles = true,
			};


		public const string TemplatesName = nameof(Templates);

		public static SettingsData<TemplateItem> Templates =
			new SettingsData<TemplateItem>($"{TemplatesName}.xml", true, null, System.Reflection.Assembly.GetExecutingAssembly())
			{
				UseSeparateFiles = true,
			};

		/// <summary>
		/// Templates available when app loads in Visual Studio.
		/// </summary>
		public static SettingsData<TemplateItem> VsTemplates
		{
			get
			{
				if (!IsVsExtension)
					return null;
				// Load but do not save.
				var solutionDir = GetSolutionDir();
				if (string.IsNullOrEmpty(solutionDir))
					return null;
				var path = System.IO.Path.Combine(solutionDir, ".config\\aicomp\\Templates");
				var data = new SettingsData<TemplateItem>($"{path}.xml", true, null, System.Reflection.Assembly.GetExecutingAssembly())
				{
					UseSeparateFiles = true,
				};
				return data;
			}
		}

		public static string GetSolutionDir()
		{
			var allProperties = _SolutionHelper.GetProperties();
			var solutionDir = allProperties.FirstOrDefault(p => p.Key == "SolutionDir").Value;
			if (!string.IsNullOrEmpty(solutionDir))
				return solutionDir;
			var solutionPath = allProperties.FirstOrDefault(p => p.Key == "SolutionPath").Value;
			if (!string.IsNullOrEmpty(solutionPath))
				return new FileInfo(solutionPath).Directory.FullName;
			return null;
		}

		public const string TasksName = nameof(Tasks);

		public static SettingsData<TemplateItem> Tasks =
			new SettingsData<TemplateItem>($"{TasksName}.xml", true, null, System.Reflection.Assembly.GetExecutingAssembly())
			{
				UseSeparateFiles = true,
			};

		public const string FineTuningsName = nameof(FineTunings);

		public static SettingsData<FineTuningItem> FineTunings =
			new SettingsData<FineTuningItem>($"{FineTuningsName}.xml", true, null, System.Reflection.Assembly.GetExecutingAssembly())
			{
				UseSeparateFiles = true,
			};

		public const string AssistantsName = nameof(Assistants);

		public static SettingsData<AssistantItem> Assistants =
			new SettingsData<AssistantItem>($"{AssistantsName}.xml", true, null, System.Reflection.Assembly.GetExecutingAssembly())
			{
				UseSeparateFiles = true,
			};

		public static ObservableCollection<string> VisibilityPaths { get; set; } = new ObservableCollection<string>();

		public static IBindingList GetSettingItems(ItemType type)
		{
			switch (type)
			{
				case ItemType.Task: return Tasks.Items;
				case ItemType.Template: return Templates.Items;
				case ItemType.FineTuning: return FineTunings.Items;
				case ItemType.Assistant: return Assistants.Items;
				case ItemType.Lists: return Lists.Items;
				case ItemType.Embeddings: return Embeddings.Items;
				case ItemType.MailAccount: return AppSettings.MailAccounts;
				case ItemType.VaultItem: return AppSettings.VaultItems;
				case ItemType.AiService: return AiServices.Items;
				case ItemType.AiModel: return AiModels.Items;
				case ItemType.UiPreset: return UiPresets.Items;
				default: return null;
			}
		}

		public static ISettingsData GetSettings(ItemType type)
		{
			switch (type)
			{
				case ItemType.Task: return Tasks;
				case ItemType.Template: return Templates;
				case ItemType.FineTuning: return FineTunings;
				case ItemType.Assistant: return Assistants;
				case ItemType.Lists: return Lists;
				case ItemType.Embeddings: return Embeddings;
				case ItemType.UiPreset: return UiPresets;
				default: return null;
			}
		}

		public static MessageBoxResult ShowError(string message, MessageBoxButton button = MessageBoxButton.OK)
		{
			var box = new MessageBoxWindow();
			box.SetSize(800, 600);
			//box.MessageTextBox.IsReadOnly = true;
			//var result = box.ShowPrompt(message, "Error!", button, MessageBoxImage.Error);
			var result = box.ShowDialog(message, "Error!", button, MessageBoxImage.Error);
			return result;
		}

		#region Keyboard Hook to handle CTRL+C

		public static void StartKeyboardHook()
		{
			if (KeyboardHook == null)
			{
				KeyboardHook = new JocysCom.ClassLibrary.Processes.KeyboardHook();
				KeyboardHook.Start(true); // Start global hook
			}
		}

		public static JocysCom.ClassLibrary.Processes.KeyboardHook KeyboardHook;

		#endregion


		public static void InsertItem(ISettingsListFileItem item, ItemType type)
		{
			if (type != ItemType.Task && type != ItemType.Template)
				return;
			AppHelper.FixName(item, Tasks.Items);
			var panel = type == ItemType.Task
				? MainControl.TasksPanel.ListPanel
				: MainControl.TemplatesPanel.ListPanel;
			panel.InsertItem(item);
		}

		public static string FineTuningPath
			=> Path.Combine(AppData.XmlFile.Directory.FullName, FineTuningsName);

		public static string AssistantsPath
			=> Path.Combine(AppData.XmlFile.Directory.FullName, AssistantsName);

		public static string PluginsSearchPath
			=> Path.Combine(AppData.XmlFile.Directory.FullName, nameof(Plugins), nameof(Plugins.Core.Search));

		public static string LogsPath
			=> Path.Combine(AppData.XmlFile.Directory.FullName, "Logs");

		public static string PluginsPath
			=> Path.Combine(AppData.XmlFile.Directory.FullName, "Plugins");


		public static string GetPath(ISettingsListFileItem item, params string[] args)
		{
			string iPath = null;
			if (item is AssistantItem)
				iPath = AssistantsPath;
			if (item is FineTuningItem)
				iPath = FineTuningPath;
			if (item is TemplateItem temp)
			{
				var itemType = Templates.Items.Contains(item)
					? iPath = TemplatesName
					: iPath = TasksName;
			}
			if (iPath == null)
				throw new NotImplementedException("Item not implemented");
			var itemPath = new string[] { AppData.XmlFile.Directory.FullName, iPath, item.Name };
			var paths = itemPath.Concat(args).ToArray();
			var path = System.IO.Path.Combine(paths);
			return path;
		}

		#region Validation

		public static void SetWithTimeout(MessageBoxImage image, string content = null, params object[] args)
		{
			MainControl.InfoPanel.SetWithTimeout(image, content, args);
		}

		public static bool ValidateServiceAndModel(IAiServiceModel am)
		{
			if (am == null)
				return false;
			var messages = new List<string>();
			if (am.AiService == null)
				messages.Add(Resources.MainResources.main_Select_AI_Service);
			if (string.IsNullOrEmpty(am.AiModel))
				messages.Add(Resources.MainResources.main_Select_AI_Model);
			if (messages.Any())
				SetWithTimeout(MessageBoxImage.Warning, string.Join(" ", messages));
			return !messages.Any();
		}

		public static async Task<bool> IsGoodSettings(AiService service, bool redirectToSettings = false)
		{
			var itemsRequired = new List<string>();
			if (AppSettings.EnableMicrosoftAccount && AppSettings.RequireToSignIn && !UserProfile.IsSignedIn)
			{
				MainControl.OptionsTabItem.IsSelected = true;
				MainControl.OptionsPanel.MicrosoftAccountsTabItem.IsSelected = true;
				MainControl.OptionsPanel.MicrosoftAccountsPanel.SignInButton.Focus();
				var message = Resources.MainResources.main_Require_To_Sign_In_Message;
				SetWithTimeout(MessageBoxImage.Warning, message);
				return false;
			}
			if (service == null)
			{
				SetWithTimeout(MessageBoxImage.Warning, Resources.MainResources.main_Select_AI_Service_from_Menu);
				return false;
			}
			if (string.IsNullOrEmpty(service.Name))
				itemsRequired.Add("Service Name");
			if (string.IsNullOrEmpty(service.BaseUrl))
				itemsRequired.Add($"Base URL for the '{service.Name}' service\"");
			// If OpenAI service then check for API Key and Organization ID.
			var baseUrl = service.BaseUrl ?? "";
			var isMicrosoft = baseUrl.Contains(".microsoft.com");
			var isOpenAi = baseUrl.Contains(".openai.com");
			if (isMicrosoft || isOpenAi)
			{
				var apiSecretKey = await MicrosoftResourceManager.Current.GetKeyVaultSecretValue(service.ApiSecretKeyVaultItemId, service.ApiSecretKey);
				if (string.IsNullOrEmpty(apiSecretKey))
					itemsRequired.Add($"API Key for the '{service.Name}' service");
			}
			if (redirectToSettings && itemsRequired.Count > 0)
				RedirectToAiService(service, itemsRequired);
			return itemsRequired.Count == 0;
		}

		public static async Task<bool> IsGoodSpeechSettings(AiService service, bool redirectToSettings = false)
		{
			var itemsRequired = new List<string>();
			var apiSecretKey = await MicrosoftResourceManager.Current.GetKeyVaultSecretValue(service.ApiSecretKeyVaultItemId, service.ApiSecretKey);
			if (string.IsNullOrEmpty(apiSecretKey))
				itemsRequired.Add($"API Key for the '{service.Name}' service");
			if (redirectToSettings && itemsRequired.Count > 0)
				RedirectToAiService(service, itemsRequired);
			return itemsRequired.Count == 0;
		}

		private static void RedirectToAiService(AiService service, List<string> itemsRequired)
		{
			var settings = AppSettings.GetTaskSettings(ItemType.AiService);
			settings.ListSelection.Clear();
			settings.ListSelection.Add(service.Name);
			MainControl.OptionsTabItem.IsSelected = true;
			MainControl.OptionsPanel.AiServicesTabItem.IsSelected = true;
			var s = string.Join(" and ", itemsRequired);
			var message = string.Format(Resources.MainResources.main_Provide_the, s);
			SetWithTimeout(MessageBoxImage.Warning, message);
		}

		#endregion

		#region Events;

		public static event EventHandler OnMainControlLoaded;

		public static event EventHandler OnSaveSettings;
		public static event EventHandler OnFilesUpdaded;
		public static event EventHandler OnFineTuningJobCreated;
		public static event EventHandler OnSourceDataFilesUpdated;
		public static event EventHandler OnTuningDataFilesUpdated;
		public static event EventHandler OnTasksUpdated;
		public static event EventHandler OnTemplatesUpdated;
		public static event EventHandler OnAiServicesUpdated;
		public static event EventHandler OnAiModelsUpdated;
		public static event EventHandler OnListsUpdated;
		public static event EventHandler OnEmbeddingsUpdated;
		public static event SelectionChangedEventHandler OnTabControlSelectionChanged;

		public static void RaiseOnMainControlLoaded()
			=> OnMainControlLoaded?.Invoke(null, EventArgs.Empty);

		public static void RaiseOnSaveSettings()
			=> _ = Helper._Debounce(OnSaveSettings, null, new[] { null, EventArgs.Empty });

		public static void RaiseOnFilesUploaded()
			=> _ = Helper._Debounce(OnFilesUpdaded, null, new[] { null, EventArgs.Empty });

		public static void RaiseOnFineTuningJobCreated()
			=> _ = Helper._Debounce(OnFineTuningJobCreated, null, new[] { null, EventArgs.Empty });

		public static void RaiseOnSourceDataFilesUpdated()
			=> _ = Helper._Debounce(OnSourceDataFilesUpdated, null, new[] { null, EventArgs.Empty });

		public static void RaiseOnTuningDataFilesUpdated()
			=> _ = Helper._Debounce(OnTuningDataFilesUpdated, null, new[] { null, EventArgs.Empty });

		public static void RaiseOnTasksUpdated()
			=> _ = Helper._Debounce(OnTasksUpdated, null, new[] { null, EventArgs.Empty });

		public static void RaiseOnTemplatesUpdated()
			=> _ = Helper._Debounce(OnTemplatesUpdated, null, new[] { null, EventArgs.Empty });

		public static void RaiseOnAiServicesUpdated()
			=> _ = Helper._Debounce(OnAiServicesUpdated, null, new[] { null, EventArgs.Empty });

		public static void RaiseOnAiModelsUpdated()
			=> _ = Helper._Debounce(OnAiModelsUpdated, null, new[] { null, EventArgs.Empty });

		public static void RaiseOnListsUpdated()
			=> _ = Helper._Debounce(OnListsUpdated, null, new[] { null, EventArgs.Empty });

		public static void RaiseOnEmbeddingsUpdated()
			=> _ = Helper._Debounce(OnEmbeddingsUpdated, null, new[] { null, EventArgs.Empty });

		public static void RaiseOnTabControlSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			OnTabControlSelectionChanged?.Invoke(sender, e);
		}

		public static void SelectTask(params string[] names)
		{
			// Select new task in the tasks list on the [Tasks] tab.
			var settings = AppSettings.GetTaskSettings(ItemType.Task);
			if (names.Length > 0)
				settings.ListSelection = names.ToList();
			settings.Focus = true;
			RaiseOnTasksUpdated();
		}

		#endregion

		public static void SaveSettings()
		{
			// Don't save if not loaded first.
			if (!IsSettignsLoaded)
				return;
			RaiseOnSaveSettings();
			AppData.Save();
			Voices.Save();
			Prompts.Save();
			Lists.Save();
			if (StoreAiServicesAndModelsInSeparateFile)
			{
				AiServices.Save();
				AiModels.Save();
			}
			Embeddings.Save();
			Templates.Save();
			Tasks.Save();
			FineTunings.Save();
			Assistants.Save();
			UiPresets.Save();
			Resets.Save();
		}

		/// <summary>
		/// Subscribed by controls that need to refresh when the source data is updated.
		/// </summary>
		public static event EventHandler PromptingUpdated;
		public static void TriggerPromptingUpdated()
			=> PromptingUpdated?.Invoke(null, EventArgs.Empty);


		/// <summary>
		/// Subscribed by controls that need to refresh when the source data is updated.
		/// </summary>
		public static event EventHandler VoicesUpdated;
		public static void TriggerVoicesUpdated()
			=> VoicesUpdated?.Invoke(null, EventArgs.Empty);


		public static bool ResetSettings = false;

		public static bool IsSettignsLoaded;

		public static bool IsAppExiting;

		public static bool IsMainWindowClosing;

		public static void LoadGlobal()
		{
			AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
			// Start settings check.
			InitUpdateTimeChecker();
		}

		private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
		{
			IsAppExiting = true;
			//UnInitUpdateTimeChecker();
		}

		public static void LoadSettings()
		{
			SqlInitHelper.SqlBatteriesInit();

			// Make sure DbContext supports SQL Server and SQLite
			SqlInitHelper.AddDbProviderFactories();
			// Set a converter to convert SVG to images for the user interface.
			SettingsListFileItem.ConvertToImage = Converters.SvgHelper.LoadSvgFromString;
			ResetSettings = false;
			// Load app data.
			AppData.OnValidateData += AppData_OnValidateData;
			AppData.Load();
			if (AppData.IsSavePending)
			{
				AppData.Save();
			}
			AppSettings.CleanupAiModels();
			AiServices.Items.ListChanged += AiServices_ListChanged;
			if (ResetSettings)
				SettingsSourceManager.ResetAllSettings();
			else
			{
				// If Azure "Speech Service" not found then...
				if (!AiServices.Items.Any(x => x.ServiceType == ApiServiceType.Azure))
				{
					// Add "Speech Service" from the settings file.
					var zip = SettingsSourceManager.GetSettingsZip();
					if (zip != null)
					{
						var zipServices = SettingsSourceManager.GetItemsFromZip(zip, Global.AiServicesName, Global.AiServices);
						var azureService = zipServices.FirstOrDefault(x => x.ServiceType == ApiServiceType.Azure);
						if (azureService != null)
						{
							AiServices.Add(azureService);
							RaiseOnAiServicesUpdated();
						}
					}
				}
			}
			InitHotKeys(Application.Current.MainWindow);
			// Always refresh plugins.
			var newPluginsList = Engine.AppData.RefreshPlugins(AppSettings.Plugins);
			ClassLibrary.Collections.CollectionsHelper.Synchronize(newPluginsList, AppSettings.Plugins);
			// Load Voice items.
			Voices.OnValidateData += Voices_OnValidateData;
			Voices.Load();
			if (Voices.IsSavePending)
				Voices.Save();
			// Load Prompt items.
			Prompts.OnValidateData += PromptItems_OnValidateData;
			Prompts.Load();
			if (Prompts.IsSavePending)
				Prompts.Save();
			// Load Lists.
			Lists.OnValidateData += Lists_OnValidateData;
			Lists.Load();
			if (Lists.IsSavePending)
				Lists.Save();
			if (StoreAiServicesAndModelsInSeparateFile)
			{
				// Load Services.
				AiServices.OnValidateData += AiServices_OnValidateData;
				AiServices.Load();
				if (AiServices.IsSavePending)
					AiServices.Save();
				// Load Models.
				AiModels.OnValidateData += AiModels_OnValidateData;
				AiModels.Load();
				if (AiModels.IsSavePending)
					AiModels.Save();
			}
			// Load Resets items.
			Resets.OnValidateData += Resets_OnValidateData;
			Resets.Load();
			if (Resets.IsSavePending)
				Resets.Save();
			// Load UI Presets.
			UiPresets.OnValidateData += UiPresets_OnValidateData;
			UiPresets.Load();
			if (UiPresets.IsSavePending)
				UiPresets.Save();
			// Load Embeddings.
			Embeddings.OnValidateData += Embeddings_OnValidateData;
			Embeddings.Load();
			Embeddings.ItemRenamed += Embeddings_ItemRenamed;
			if (Embeddings.IsSavePending)
				Embeddings.Save();
			// Bind list to plugins.
			Plugins.Core.Lists.AllLists = Lists.Items;
			// Load templates.
			Templates.OnValidateData += Templates_OnValidateData;
			Templates.Load();
			if (Templates.IsSavePending)
				Templates.Save();
			// Load tasks.
			Tasks.OnValidateData += Tasks_OnValidateData;
			Tasks.Load();
			if (Tasks.IsSavePending)
				Tasks.Save();
			// Load fine tune settings.
			FineTunings.OnValidateData += FineTunings_OnValidateData;
			FineTunings.Load();
			if (FineTunings.IsLoadPending)
				FineTunings.Load();
			// Load Assistant settings.
			Assistants.OnValidateData += Assistants_OnValidateData;
			Assistants.Load();
			if (Assistants.IsLoadPending)
				Assistants.Load();
			// Enable template and task folder monitoring.
			Templates.SetFileMonitoring(true);
			Tasks.SetFileMonitoring(true);
			// If old settings version then reset templates.
			if (AppData.Version < 2 && !ResetSettings)
			{
				AppData.Version = 2;
				SettingsSourceManager.ResetTemplates();
			}
			// Enable logging.
			AppSettings.PropertyChanged += AppSettings_PropertyChanged;
			LogHelper.LogHttp = AppSettings.LogHttp;
			LoadGlobal();
			// Mark settings as loaded.
			IsSettignsLoaded = true;
		}

		private static void AppSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(Engine.AppData.LogHttp))
				LogHelper.LogHttp = AppSettings.LogHttp;
			if (e.PropertyName == nameof(Engine.AppData.UiPresetName))
			{
				// Apply new prest to controls.
				UiPresetsManager.ApplyUiPreset(AppSettings.UiPresetName, UiPresetsManager.AllUiElements.Keys.ToArray());
			}
			if (e.PropertyName == nameof(Engine.AppData.UiTheme))
			{
				ThemeHelper.SwitchAppTheme(AppSettings.UiTheme);
			}
			if (e.PropertyName == nameof(Engine.AppData.AiWindowHotKey) || e.PropertyName == nameof(Engine.AppData.AiWindowHotKeyEnabled))
				UpdateAiWindowHotKey();
		}

		private static void AiServices_ListChanged(object sender, ListChangedEventArgs e)
		{
			AppHelper.CollectionChanged(e, RaiseOnAiServicesUpdated,
				nameof(AiService.Name), nameof(AiService.ServiceType));
		}

		private static void Embeddings_ItemRenamed(object sender, SettingsData<EmbeddingsItem>.ItemPropertyChangedEventArgs e)
		{
			var item = (EmbeddingsItem)sender;
			var oldFullName = AssemblyInfo.ExpandPath(item.Target);
			// If not a file path then return (probably connection string)
			if (!SqlInitHelper.IsPortable(oldFullName))
				return;
			var oldFileBaseName = Path.GetFileNameWithoutExtension(oldFullName);
			// if old file name did not match new item name then return.
			if (!oldFileBaseName.Equals((string)e.OldValue, StringComparison.OrdinalIgnoreCase))
				return;
			var ext = Path.GetExtension(oldFullName);
			var fi = new FileInfo(oldFullName);
			var newFullName = Path.Combine(fi.Directory.FullName, (string)e.NewValue + ext);
			if (fi.Exists)
			{
				try
				{
					fi.MoveTo(newFullName);
				}
				catch (Exception ex)
				{
					MainControl.ErrorsPanel.ErrorsLogPanel.Add(ex.ToString() + "\r\n");
					return;
				}
			}
			item.Target = AssemblyInfo.ParameterizePath(newFullName, true);
		}

		private static void Assistants_OnValidateData(object sender, SettingsData<AssistantItem>.SettingsDataEventArgs e)
		{
			var data = (SettingsData<AssistantItem>)sender;
			if (e.Items.Count != 0)
				return;
		}

		private static void FineTunings_OnValidateData(object sender, SettingsData<FineTuningItem>.SettingsDataEventArgs e)
		{
			var data = (SettingsData<FineTuningItem>)sender;
			if (e.Items.Count != 0)
				return;
			var zip = SettingsSourceManager.GetSettingsZip();
			if (zip != null)
			{
				var targetPath = AppData.XmlFile.Directory.FullName;
				AppHelper.ExtractFiles(zip, targetPath, @"^FineTuning\\");
				// Trigger reload of data.
				data.IsLoadPending = true;
			}
		}

		class prompt_item
		{
			public string title { get; set; }
			public string pattern { get; set; }
			public string[] options { get; set; }
		}

		private static void Voices_OnValidateData(object sender, SettingsData<VoiceItem>.SettingsDataEventArgs e)
		{
			if (e.Items.Count == 0)
			{
				SettingsSourceManager.ResetVoices();
				// Data is reset, no need to handle it.
				e.Handled = true;
			}
		}

		private static void PromptItems_OnValidateData(object sender, SettingsData<PromptItem>.SettingsDataEventArgs e)
		{
			if (e.Items.Count == 0)
			{
				SettingsSourceManager.ResetPrompts();
				// Data is reset, no need to handle it.
				e.Handled = true;
			}
		}

		private static void Lists_OnValidateData(object sender, SettingsData<ListInfo>.SettingsDataEventArgs e)
		{
			if (e.Items.Count == 0)
			{
				SettingsSourceManager.ResetLists();
				// Data is reset, no need to handle it.
				e.Handled = true;
			}
			else
			{
				// Check for missing templates only.
				var itemsAdded = SettingsSourceManager.CheckRequiredItems(e.Items);
				if (itemsAdded > 0)
				{
					// Reorder and save.
					Lists.SortList(e.Items);
					Lists.IsSavePending = true;
				}
			}
		}

		public static void FixAiServices(IList<AiService> items)
		{
			if (items == null)
				return;
			// Fix filters.
			foreach (var item in items)
			{
				if (string.IsNullOrWhiteSpace(item.ModelFilter))
					continue;
				var filters = item.ModelFilter.Split('|').ToList();
				if (filters.Contains("embedding"))
					continue;
				filters.Add("embedding");
				item.ModelFilter = string.Join("|", filters);
			}
		}

		private static void AiServices_OnValidateData(object sender, SettingsData<AiService>.SettingsDataEventArgs e)
		{
			if (e.Items.Count == 0)
			{
				SettingsSourceManager.ResetServicesAndModels();
				// Data is reset, no need to handle it.
				e.Handled = true;
			}
			else
			{
				FixAiServices(e.Items);
			}
		}

		public static void FixAiModels(IList<AiModel> items)
		{
			if (items == null)
				return;
			foreach (var item in items)
			{
				if (item.MaxInputTokens == 0)
					item.MaxInputTokens = Companions.ChatGPT.Client.GetMaxInputTokens(item.Name);
			}
		}


		private static void AiModels_OnValidateData(object sender, SettingsData<AiModel>.SettingsDataEventArgs e)
		{
			if (e.Items.Count == 0)
			{
				SettingsSourceManager.ResetServicesAndModels();
				// Data is reset, no need to handle it.
				e.Handled = true;
			}
			else
			{
				FixAiModels(e.Items);
			}
		}


		private static void Resets_OnValidateData(object sender, SettingsData<ListInfo>.SettingsDataEventArgs e)
		{
			if (e.Items.Count == 0)
			{
				SettingsSourceManager.ResetResets();
				// Data is reset, no need to handle it.
				e.Handled = true;
			}
			else
			{
			}
		}

		private static void UiPresets_OnValidateData(object sender, SettingsData<UiPresetItem>.SettingsDataEventArgs e)
		{
			if (e.Items.Count == 0)
			{
				SettingsSourceManager.ResetUiPresets();
				// Data is reset, no need to handle it.
				e.Handled = true;
			}
			else
			{
				// Check for missing templates only.
				//var itemsAdded = SettingsSourceManager.CheckRequiredItems(e.Items);
				//if (itemsAdded > 0)
				//{
				//	// Reorder and save.
				//	Lists.SortList(e.Items);
				//	Lists.IsSavePending = true;
				//}
			}
		}

		private static void Embeddings_OnValidateData(object sender, SettingsData<EmbeddingsItem>.SettingsDataEventArgs e)
		{
			if (e.Items.Count == 0)
			{
				SettingsSourceManager.ResetEmbeddings();
				// Data is reset, no need to handle it.
				e.Handled = true;
			}
			else
			{
				// Check for missing templates only.
				var itemsAdded = SettingsSourceManager.CheckRequiredItems(e.Items);
				if (itemsAdded > 0)
				{
					// Reorder and save.
					Embeddings.SortList(e.Items);
					Embeddings.IsSavePending = true;
				}
			}
		}

		class IHasGuidComparer : IEqualityComparer<IHasGuid>
		{
			public bool Equals(IHasGuid x, IHasGuid y) => x.Id == y.Id;
			public int GetHashCode(IHasGuid obj) => throw new System.NotImplementedException();
		}

		private static void AppData_OnValidateData(object sender, SettingsData<AppData>.SettingsDataEventArgs e)
		{
			var data = sender as ISettingsData;
			// If no setting found then...
			if (e.Items.Count == 0)
			{
				e.Items.Add(new AppData());
				data.IsSavePending = true;
				ResetSettings = true;
			}

			var aiServices = e.Items.FirstOrDefault()?.AiServices;
			var aiModels = e.Items.FirstOrDefault()?.AiModels;

			if (StoreAiServicesAndModelsInSeparateFile)
			{
				// Move AI services to a new location / file.
				if (aiServices?.Any() == true && !AiServices.Items.Any())
				{
					CollectionsHelper.Synchronize(aiServices, AiServices.Items, new IHasGuidComparer());
					AiServices.Save();
					// Don't clear yet, in order not to break old apps.
					//e.Items.Clear();
				}
				// Move AI Models to a new location / file.
				if (aiModels?.Any() == true && !AiModels.Items.Any())
				{
					CollectionsHelper.Synchronize(aiModels, AiModels.Items, new IHasGuidComparer());
					AiModels.Save();
					// Don't clear yet, in order not to break old apps.
					//e.Items.Clear();
				}
			}
			else
			{
				AiServices.Items = aiServices;
				AiModels.Items = aiModels;
				if (!aiServices.Any() || !aiModels.Any())
					SettingsSourceManager.ResetServicesAndModels();
				FixAiServices(aiServices);
				FixAiModels(aiModels);
			}

			var avatarItem = e.Items.FirstOrDefault()?.AiAvatar;
			if (avatarItem != null)
			{
				if (string.IsNullOrEmpty(avatarItem.Message))
					avatarItem.Message = Engine.Resources.MainResources.main_AvatarItem_Message;
				if (string.IsNullOrEmpty(avatarItem.Instructions))
					avatarItem.Instructions = Engine.Resources.MainResources.main_AvatarItem_Instructions;
			}
			var userProfiles = e.Items.FirstOrDefault()?.UserProfiles;
			if (userProfiles != null)
			{
				// Don't allow multiple user profiles at the moment.
				while (userProfiles.Count() > 1)
					userProfiles.RemoveAt(0);
				if (userProfiles.Count == 0)
					userProfiles.Add(new UserProfile());
				// Cleanup duplicates.
				var tokens = userProfiles.First().Tokens;
				var keys = tokens.Select(x => x.Key).Distinct().ToArray();
				foreach (var key in keys)
				{
					var dupes = tokens.Where(x => x.Key == key).Skip(1);
					foreach (var dupe in dupes)
						tokens.Remove(dupe);
				}
			}
			// Fix panel settings.
			var panelSettings = e.Items.FirstOrDefault()?.PanelSettingsList;
			if (userProfiles != null)
			{
				var types = (ItemType[])Enum.GetValues(typeof(ItemType));
				var uniqeItems = types.SelectMany(x => panelSettings.Where(s => s.ItemType == x).Take(1)).ToArray();
				var itemsToRemove = panelSettings.Except(uniqeItems).ToArray();
				foreach (var item in itemsToRemove)
					panelSettings.Remove(item);
			}
		}

		private static bool FixTempalteItems(IList<TemplateItem> items)
		{
			// Fix items with wrong AI Serivce Id (empty service name).
			var emptyServiceGuid = AppHelper.GetGuid(nameof(AiService), "");
			var tasks = items.Where(x => x.AiServiceId == emptyServiceGuid).ToArray();
			foreach (var task in tasks)
				task.AiServiceId = SettingsSourceManager.OpenAiServiceId;
			return tasks.Count() > 0;
		}

		private static void Templates_OnValidateData(object sender, SettingsData<TemplateItem>.SettingsDataEventArgs e)
		{
			var data = (SettingsData<TemplateItem>)sender;
			if (e.Items.Count == 0)
			{
				var zip = SettingsSourceManager.GetSettingsZip();
				if (zip != null)
				{
					var items = SettingsSourceManager.GetItemsFromZip(zip, TemplatesName, Templates);
					foreach (var item in items)
						e.Items.Add(item);
					if (items.Count > 0)
						Templates.IsSavePending = true;
				}
			}
			else
			{
				// Check for missing templates only.
				var itemsAdded = SettingsSourceManager.CheckRequiredTemplates(e.Items);
				if (itemsAdded > 0)
				{
					// Reorder and save.
					Templates.SortList(e.Items);
					Templates.IsSavePending = true;
				}
			}
			data.IsSavePending |= FixTempalteItems(e.Items);
		}

		private static void MakeSureTempalteExists(SettingsData<TemplateItem> data, IList<TemplateItem> items, params string[] names)
		{
			var asm = typeof(Global).Assembly;
			var resourceNames = asm.GetManifestResourceNames();
			foreach (var name in names)
			{
				// Check reserved templates used for some automation.
				var rItem = items.FirstOrDefault(x => x.Name == name);
				if (rItem == null)
				{
					var keys = resourceNames.Where(x => x.Contains(name)).ToList();
					foreach (var key in keys)
					{
						var bytes = Helper.GetResource<byte[]>(key, asm);
						var item = data.DeserializeItem(bytes, false);
						items.Add(item);
					}
					data.IsSavePending = true;
				}
			}
		}

		private static void Tasks_OnValidateData(object sender, SettingsData<TemplateItem>.SettingsDataEventArgs e)
		{
			var sd = (SettingsData<TemplateItem>)sender;
			if (e.Items.Count == 0)
			{
				var zip = SettingsSourceManager.GetSettingsZip();
				if (zip != null)
				{
					var items = SettingsSourceManager.GetItemsFromZip(zip, TasksName, Tasks);
					foreach (var item in items)
						e.Items.Add(item);
					if (items.Count > 0)
						Tasks.IsSavePending = true;
				}
			}
			sd.IsSavePending |= FixTempalteItems(e.Items);
		}
		public static void ClearItems()
		{
			Templates.Items.Clear();
			Tasks.Items.Clear();
		}

		public static MainControl MainControl;
		public static bool IsVsExtension { get; set; }
		public static Version VsVersion { get; set; }
		public static bool ShowExtensionVersionMessageOnError;

		#region Avatar Panel

		public static Controls.Options.AvatarControl AvatarOptionsPanel
			=> MainControl?.OptionsPanel?.AvatarOptionsPanel;

		public static AvatarControl AvatarPanel
		{
			get
			{
				if (_AvatarPanel == null)
				{
					_AvatarPanel = new AvatarControl();
					_AvatarPanel.VerticalAlignment = VerticalAlignment.Top;
					_AvatarPanel.MouseDoubleClick += _AvatarPanel_MouseDoubleClick;
				}
				return _AvatarPanel;
			}
		}

		private static void _AvatarPanel_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			var ap = AvatarPanel;
			var avatarWindow = VisualTreeHelper.GetParent(ap) as Window;
			// If avatar in window already, return.
			if (avatarWindow != null)
				return;
			MoveToWindowToggle();
		}

		static AvatarControl _AvatarPanel;

		public static Window AvatarWindow
		{
			get
			{
				if (_AvatarWindow == null)
				{
					var window = new Window
					{
						Title = "Avatar",
						Height = 640,
						Width = 360,
						Background = Brushes.Black,
						HorizontalContentAlignment = HorizontalAlignment.Center,
						VerticalContentAlignment = VerticalAlignment.Center,
					};
					window.Content = new Border();
					window.Closing += (s, e) =>
					{
						if (!IsMainWindowClosing)
						{
							e.Cancel = true;
							MoveToWindowToggle();
						}
					};
					AppSettings.AiAvatar.PropertyChanged += AiAvatar_PropertyChanged;
					_AvatarWindow = window;
					UpdateAlwaysOnTop();
				}
				return _AvatarWindow;
			}
		}

		static Window _AvatarWindow;

		public static bool IsAvatarInWindow;

		public static Border AvatarBorder { get; set; }

		public static readonly object avatarLock = new object();

		public static void UpdateAvatarControl(Border avatarBorder, bool show)
		{
			lock (avatarLock)
			{
				// Store last avatar border.
				AvatarBorder = avatarBorder;
				var ap = AvatarPanel;
				if (IsAvatarInWindow)
					return;
				avatarBorder.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
				// If must show avatar and border is visible, move avatar panel into it.
				if (show && ControlsHelper.IsTabItemSelected(avatarBorder))
				{
					ControlsHelper.RemoveFromParent(ap);
					if (avatarBorder.Child != ap)
						avatarBorder.Child = ap;
				}
			}
		}

		public static void MoveToWindowToggle()
		{
			lock (avatarLock)
			{
				var ap = AvatarPanel;
				ControlsHelper.RemoveFromParent(ap);
				// If avatar in Window already...
				if (IsAvatarInWindow)
				{
					IsAvatarInWindow = false;
					// Close and remove from window.
					AvatarWindow.Hide();
					AvatarBorder.Child = ap;
					AvatarBorder.Visibility = Visibility.Visible;
				}
				else
				{
					IsAvatarInWindow = true;
					AvatarBorder.Visibility = Visibility.Collapsed;
					(AvatarWindow.Content as Border).Child = ap;
					AvatarWindow.Show();
				}
			}
		}

		private static void AvatarPanel_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			MoveToWindowToggle();
		}

		public static void UpdateAlwaysOnTop()
		{
			if (AvatarWindow != null)
				AvatarWindow.Topmost = AppSettings.AiAvatar.AlwaysOnTop;
		}


		private static void AiAvatar_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(AvatarItem.AlwaysOnTop))
				UpdateAlwaysOnTop();
		}

		#endregion

	}
}
