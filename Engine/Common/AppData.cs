using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Controls.UpdateControl;
using JocysCom.VS.AiCompanion.Engine.Security;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	/// <summary>
	/// Application settings.
	/// </summary>
	/// <remarks>Advice: Organize all settings as flat data tables instead of using a tree structure to improve compatibility and ease conversion.</remarks>
	public class AppData : SettingsItem, INotifyPropertyChanged, ITrayManagerSettings
	{

		public AppData()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
		}

		#region Microsoft Account (Enterprise settings)

		/// <summary>
		/// Azure: Home / App registrations: Jocys.com AI Companion
		/// </summary>
		[DefaultValue("6786bf7e-0379-43e9-8ab6-c10326af0123")]
		public string AppClientId { get => _AppClientId; set => SetProperty(ref _AppClientId, value); }
		private string _AppClientId;

		/// <summary>
		/// Azure: Home / App registrations: Jocys.com AI Companion
		/// Tenand ID of Microsoft Entra ID (formerly known as Azure Active Directory)
		/// Azure: Home / Microsoft Entra ID / Overview / Tenant ID: ...
		/// </summary>
		[DefaultValue(null)]
		public string AppTenantId { get => _AppTenantId; set => SetProperty(ref _AppTenantId, value); }
		private string _AppTenantId;

		/// <summary>
		/// Enable support for microsoft account.
		/// </summary>
		[DefaultValue(false)]
		public bool EnableMicrosoftAccount { get => _EnableMicrosoftAccount; set => SetProperty(ref _EnableMicrosoftAccount, value); }
		private bool _EnableMicrosoftAccount;

		/// <summary>
		/// Require to sign in when microsoft account is enabled.
		/// </summary>
		[DefaultValue(false)]
		public bool RequireToSignIn { get => _RequireToSignIn; set => SetProperty(ref _RequireToSignIn, value); }
		private bool _RequireToSignIn;

		#endregion

		[DefaultValue(false)]
		public bool AppAlwaysOnTop { get => _AppAlwaysOnTop; set => SetProperty(ref _AppAlwaysOnTop, value); }
		private bool _AppAlwaysOnTop;

		/// <summary>Allow only one standalone copy.</summary>
		[DefaultValue(true)]
		public bool AllowOnlyOneCopy { get => _AllowOnlyOneCopy; set => SetProperty(ref _AllowOnlyOneCopy, value); }
		private bool _AllowOnlyOneCopy;

		[DefaultValue(false)]
		public bool BarAlwaysOnTop { get => _BarAlwaysOnTop; set => SetProperty(ref _BarAlwaysOnTop, value); }
		private bool _BarAlwaysOnTop;


		[DefaultValue(RiskLevel.Critical)]
		public RiskLevel MaxRiskLevel { get => _MaxRiskLevel; set => SetProperty(ref _MaxRiskLevel, value); }
		RiskLevel _MaxRiskLevel;

		[DefaultValue(RiskLevel.Critical)]
		public RiskLevel MaxRiskLevelWhenSignedOut { get => _MaxRiskLevelWhenSignedOut; set => SetProperty(ref _MaxRiskLevelWhenSignedOut, value); }
		RiskLevel _MaxRiskLevelWhenSignedOut;

		[DefaultValue(false)]
		public bool IsEnterprise { get => _IsEnterprise; set => SetProperty(ref _IsEnterprise, value); }
		private bool _IsEnterprise;

		[DefaultValue("https://github.com/JocysCom/VsAiCompanion/raw/main/Engine/Resources/Settings.CompanyName.zip")]
		public string ConfigurationUrl { get => _ConfigurationUrl; set => SetProperty(ref _ConfigurationUrl, value); }
		private string _ConfigurationUrl;

		[DefaultValue(null)]
		public string VideoInputDevice { get => _VideoInputDevice; set => SetProperty(ref _VideoInputDevice, value); }
		private string _VideoInputDevice;

		[DefaultValue(null)]
		public AvatarItem AiAvatar
		{
			get => _AiAvatar = _AiAvatar ?? new AvatarItem();
			set => SetProperty(ref _AiAvatar, value);
		}
		private AvatarItem _AiAvatar;

		public UpdateSettings UpdateSettings
		{
			get => _UpdateSettings = _UpdateSettings ?? new UpdateSettings();
			set => _UpdateSettings = value;
		}
		private UpdateSettings _UpdateSettings;

		public UpdateSettings PandocUpdateSettings
		{
			get => _PandocUpdateSettings = _PandocUpdateSettings ?? new UpdateSettings();
			set => _PandocUpdateSettings = value;
		}
		private UpdateSettings _PandocUpdateSettings;

		#region Positions and Locations

		public PositionSettings StartPosition
		{
			get => _StartPosition = _StartPosition ?? new PositionSettings() { Width = 900, Height = 800 };
			set => _StartPosition = value;
		}
		private PositionSettings _StartPosition;

		public List<TaskSettings> PanelSettingsList
		{
			get => _PanelSettingsList = _PanelSettingsList ?? new List<TaskSettings>();
			set => SetProperty(ref _PanelSettingsList, value);
		}
		private List<TaskSettings> _PanelSettingsList;


		public TaskSettings GetTaskSettings(ItemType type)
		{

			var item = PanelSettingsList.FirstOrDefault(x => x.ItemType == type);
			if (item == null)
			{
				item = new TaskSettings() { ItemType = type };
				PanelSettingsList.Add(item);
			}
			return item;
		}

		#endregion

		#region Avatar

		[DefaultValue(false)]
		public bool ShowAvatar { get => _ShowAvatar; set => SetProperty(ref _ShowAvatar, value); }
		private bool _ShowAvatar;

		[DefaultValue(WindowState.Normal)]
		public WindowState AvatarWindowsState { get => _AvatarWindowsState; set => SetProperty(ref _AvatarWindowsState, value); }
		WindowState _AvatarWindowsState;

		#endregion

		#region User Profiles

		/// <summary>User Profiles</summary>
		public SortableBindingList<UserProfile> UserProfiles
		{
			get => _UserProfiles.Value;
			set => Interlocked.Exchange(ref _UserProfiles, new Lazy<SortableBindingList<UserProfile>>(() => value));
		}
		private volatile Lazy<SortableBindingList<UserProfile>> _UserProfiles =
			new Lazy<SortableBindingList<UserProfile>>(() => new SortableBindingList<UserProfile>());

		public bool ShouldSerializeUserProfiles() => UserProfiles.Count > 0;

		#endregion

		#region Vault Items

		/// <summary>Vault Items</summary>
		public SortableBindingList<VaultItem> VaultItems
		{
			get => _VaultItems.Value;
			set => Interlocked.Exchange(ref _VaultItems, new Lazy<SortableBindingList<VaultItem>>(() => value));
		}
		private volatile Lazy<SortableBindingList<VaultItem>> _VaultItems =
			new Lazy<SortableBindingList<VaultItem>>(() => new SortableBindingList<VaultItem>());

		public bool ShouldSerializeVaultItems() => VaultItems?.Count > 0;

		#endregion

		/// <summary>Azure access token cache to make it persistend during app restarts.</summary>
		[XmlIgnore, JsonIgnore]
		public byte[] AzureTokenCache
		{
			get => _AzureTokenCacheEncrypted is null
				? null
				: AppHelper.UserDecrypt(Convert.FromBase64String(_AzureTokenCacheEncrypted));
			set
			{
				_AzureTokenCacheEncrypted = value is null
					? null
					: Convert.ToBase64String(AppHelper.UserEncrypt(value)); OnPropertyChanged();
			}
		}

		[DefaultValue(null), XmlElement(ElementName = nameof(AzureTokenCache))]
		public string _AzureTokenCacheEncrypted { get; set; }

		#region Product

		[DefaultValue(null)]
		public string OverrideInfoDefaultHead { get => _OverrideInfoDefaultHead; set => SetProperty(ref _OverrideInfoDefaultHead, value); }
		private string _OverrideInfoDefaultHead;

		[DefaultValue(null)]
		public string OverrideInfoDefaultBody { get => _OverrideInfoDefaultBody; set => SetProperty(ref _OverrideInfoDefaultBody, value); }
		private string _OverrideInfoDefaultBody;

		#endregion


		#region IO Presets

		/// <summary>UI Preset</summary>
		[DefaultValue("Advanced")]
		public string UiPresetName { get => _UiPresetName; set => SetProperty(ref _UiPresetName, value); }
		private string _UiPresetName;

		#endregion

		#region Reset Settings

		[DefaultValue(1020)]
		public int ResetWindowWidth { get => _ResetSizeWidth; set => SetProperty(ref _ResetSizeWidth, value); }
		private int _ResetSizeWidth;

		[DefaultValue(780)]
		public int ResetWindowHeight { get => _ResetSizeHeight; set => SetProperty(ref _ResetSizeHeight, value); }
		private int _ResetSizeHeight;

		[DefaultValue(false)]
		public bool ResetTasksMirror { get => _ResetTasksMirror; set => SetProperty(ref _ResetTasksMirror, value); }
		private bool _ResetTasksMirror;

		[DefaultValue(true)]
		public bool ResetTemplatesMirror { get => _ResetTemplatesMirror; set => SetProperty(ref _ResetTemplatesMirror, value); }
		private bool _ResetTemplatesMirror;

		[DefaultValue(true)]
		public bool ResetPromptsMirror { get => _ResetPromptsMirror; set => SetProperty(ref _ResetPromptsMirror, value); }
		private bool _ResetPromptsMirror;

		[DefaultValue(true)]
		public bool ResetVoicesMirror { get => _ResetVoicesMirror; set => SetProperty(ref _ResetVoicesMirror, value); }
		private bool _ResetVoicesMirror;

		[DefaultValue(true)]
		public bool ResetListsMirror { get => _ResetListsMirror; set => SetProperty(ref _ResetListsMirror, value); }
		private bool _ResetListsMirror;

		[DefaultValue(true)]
		public bool ResetEmbeddingsMirror { get => _ResetEmbeddingsMirror; set => SetProperty(ref _ResetEmbeddingsMirror, value); }
		private bool _ResetEmbeddingsMirror;

		[DefaultValue(true)]
		public bool ResetUiPresetsMirror { get => _ResetUiPresetsMirror; set => SetProperty(ref _ResetUiPresetsMirror, value); }
		private bool _ResetUiPresetsMirror;

		#endregion

		#region  Spell Check

		/// <summary>Allow only one standalone copy.</summary>
		[DefaultValue(false)]
		public bool IsSpellCheckEnabled { get => _IsSpellCheckEnabled; set => SetProperty(ref _IsSpellCheckEnabled, value); }
		private bool _IsSpellCheckEnabled;

		#endregion

		#region Attached Context Titles

		[DefaultValue("Data for Processing")]
		public string ContextDataTitle { get => _ContextDataTitle; set => SetProperty(ref _ContextDataTitle, value); }
		private string _ContextDataTitle;

		[DefaultValue("Files for Processing")]
		public string ContextFileTitle { get => _ContextFileTitle; set => SetProperty(ref _ContextFileTitle, value); }
		private string _ContextFileTitle;

		[DefaultValue("Chat Log")]
		public string ContextChatTitle { get => _ContextChatTitle; set => SetProperty(ref _ContextChatTitle, value); }
		private string _ContextChatTitle;

		[DefaultValue("The chat log contains our previous conversation. Please provide a relevant and coherent response based on chat log.")]
		public string ContextChatInstructions { get => _ContextChatInstructions; set => SetProperty(ref _ContextChatInstructions, value); }
		private string _ContextChatInstructions;

		public const string _ContextFunctionRequestInstructionsText = @"If you need to call a function, you can use one of the available functions listed below:";

		public const string ContextFunctionResponseInstructionsText = @"To call a function, Provide the function call as JSON that strictly adheres to the JSON schema for function calls, which is supplied below.
Enclose the JSON code in triple backticks with ""JSON"" after the opening backticks, like this:
```JSON
{ your JSON here }
```
Do not mention the function call outside of the JSON code block.
Do not include any extra text before or after the JSON code block.
Ensure the JSON conforms exactly to the provided schema below:";


		[DefaultValue(_ContextFunctionRequestInstructionsText)]
		public string ContextFunctionRequestInstructions { get => _ContextFunctionRequestInstructions; set => SetProperty(ref _ContextFunctionRequestInstructions, value); }
		private string _ContextFunctionRequestInstructions;


		[DefaultValue(ContextFunctionResponseInstructionsText)]
		public string ContextFunctionResponseInstructions { get => _ContextFunctionResponseInstructions; set => SetProperty(ref _ContextFunctionResponseInstructions, value); }
		private string _ContextFunctionResponseInstructions;


		/// <summary>Instructions that will be included at the start of every message.</summary>
		[DefaultValue("")]
		public string GlobalInstructions { get => _GlobalInstructions; set => SetProperty(ref _GlobalInstructions, value); }
		string _GlobalInstructions;

		/// <summary>Instructions that will be use to analyse images as structured JSON.</summary>
		[DefaultValue(
@"You are an AI assistant that extracts data from documents and returns them as structured JSON objects.
Ensure that all attribute names are consistently formatted in snake_case.
Maintain uniform terminology and capitalization for all extracted values.
Provide detailed and clear structures without ambiguity.
Do not return as a code block.")]
		public string StructuredImageAnalysisInstructions { get => _StructuredImageAnalysisInstructions; set => SetProperty(ref _StructuredImageAnalysisInstructions, value); }
		string _StructuredImageAnalysisInstructions;

		[DefaultValue("Error for Processing")]
		public string ContextErrorTitle { get => _ContextErrorTitle; set => SetProperty(ref _ContextErrorTitle, value); }
		private string _ContextErrorTitle;

		[DefaultValue(true)]
		public bool ShowDocumentsAttachedWarning { get => _ShowDocumentsAttachedWarning; set => SetProperty(ref _ShowDocumentsAttachedWarning, value); }
		private bool _ShowDocumentsAttachedWarning;

		[DefaultValue(true)]
		public bool ShowSensitiveDataWarning { get => _ShowSensitiveDataWarning; set => SetProperty(ref _ShowSensitiveDataWarning, value); }
		private bool _ShowSensitiveDataWarning;

		[DefaultValue(true)]
		public bool UseEnterToSendMessage { get => _UseEnterToSendMessage; set => SetProperty(ref _UseEnterToSendMessage, value); }
		private bool _UseEnterToSendMessage;

		#endregion


		#region ■ Options: Developing

		[DefaultValue(false), Description("Enable Form Info (CTRL+SHIFT+RMB)")]
		public bool EnableShowFormInfo { get => _EnableShowFormInfo; set => SetProperty(ref _EnableShowFormInfo, value); }
		bool _EnableShowFormInfo;

		[DefaultValue(false)]
		public bool ShowErrorsPanel { get => _ShowErrorsPanel; set => SetProperty(ref _ShowErrorsPanel, value); }
		bool _ShowErrorsPanel;

		[DefaultValue(false)]
		public bool LogHttp { get => _LogHttp; set => SetProperty(ref _LogHttp, value); }
		bool _LogHttp;

		#endregion

		/// <summary>
		/// Remove models without services.
		/// </summary>
		public void CleanupAiModels()
		{
			var serviceIds = AiServices.Select(x => x.Id).ToArray();
			// Fix: Remove with empty name
			var modelsToRemove = Global.AppSettings.AiModels.Where(x => string.IsNullOrWhiteSpace(x.Name)).ToArray();
			foreach (var model in modelsToRemove)
				AiModels.Remove(model);
			// Remove models without services.
			modelsToRemove = AiModels.Where(x => !serviceIds.Contains(x.AiServiceId)).ToArray();
			foreach (var model in modelsToRemove)
				AiModels.Remove(model);
			// Remove duplicates
			var modelsToKeep = AiModels
				.GroupBy(model => new { model.AiServiceId, model.Name })
				.Select(group => group.First())
				.ToList();
			modelsToRemove = AiModels.Except(modelsToKeep).ToArray();
			foreach (var model in modelsToRemove)
				AiModels.Remove(model);
			// Remove duplicates
			modelsToKeep = AiModels
				.GroupBy(model => new { model.Id })
				.Select(group => group.First())
				.ToList();
			modelsToRemove = AiModels.Except(modelsToKeep).ToArray();
			foreach (var model in modelsToRemove)
				AiModels.Remove(model);
			// Fix path property (use for the list only).
			var models = AiModels.ToArray();
			foreach (var model in models)
				if (model.Path != model.AiServiceName)
					model.Path = model.AiServiceName;
		}

		/// <summary>AI Services</summary>
		public SortableBindingList<AiService> AiServices
		{
			get => _AiServices.Value;
			set => Interlocked.Exchange(ref _AiServices, new Lazy<SortableBindingList<AiService>>(() => value));
		}
		private volatile Lazy<SortableBindingList<AiService>> _AiServices =
			new Lazy<SortableBindingList<AiService>>(() => new SortableBindingList<AiService>());


		/// <summary>AI Models</summary>
		public SortableBindingList<AiModel> AiModels
		{
			get => _AiModels.Value;
			set => Interlocked.Exchange(ref _AiModels, new Lazy<SortableBindingList<AiModel>>(() => value));
		}
		private volatile Lazy<SortableBindingList<AiModel>> _AiModels =
			new Lazy<SortableBindingList<AiModel>>(() => new SortableBindingList<AiModel>());


		/// <summary>Mail Accounts</summary>
		public SortableBindingList<MailAccount> MailAccounts
		{
			get => _MailAccounts.Value;
			set => Interlocked.Exchange(ref _MailAccounts, new Lazy<SortableBindingList<MailAccount>>(() => value));
		}
		private volatile Lazy<SortableBindingList<MailAccount>> _MailAccounts =
			new Lazy<SortableBindingList<MailAccount>>(() => new SortableBindingList<MailAccount>());


		[DefaultValue(false)]
		public bool EnableApiPlugins { get => _EnableExternalPlugins; set => SetProperty(ref _EnableExternalPlugins, value); }
		private bool _EnableExternalPlugins;

		/// <summary>Plugins</summary>
		public SortableBindingList<PluginItem> Plugins
		{
			get => _Plugins.Value;
			set => Interlocked.Exchange(ref _Plugins, new Lazy<SortableBindingList<PluginItem>>(() => value));
		}
		private volatile Lazy<SortableBindingList<PluginItem>> _Plugins =
			new Lazy<SortableBindingList<PluginItem>>(() => new SortableBindingList<PluginItem>());

		[DefaultValue(20)]
		public int MaxTaskItemsInTray { get => _MaxTaskItemsInTray; set => SetProperty(ref _MaxTaskItemsInTray, value); }
		int _MaxTaskItemsInTray;

		#region AI Window

		[DefaultValue(false)]
		public bool AiWindowHotKeyEnabled { get => _AiWindowHotKeyEnabled; set => SetProperty(ref _AiWindowHotKeyEnabled, value); }
		bool _AiWindowHotKeyEnabled;

		[DefaultValue("CTRL+SHIFT+D1")]
		public string AiWindowHotKey { get => _AiWindowHotKey; set => SetProperty(ref _AiWindowHotKey, value); }
		string _AiWindowHotKey;

		#endregion

		#region ■ ITrayManagerSettings

		[DefaultValue(false)]
		public bool StartWithWindows { get => _StartWithWindows; set => SetProperty(ref _StartWithWindows, value); }
		bool _StartWithWindows;

		[DefaultValue(true)]
		public bool MinimizeToTray { get => _MinimizeToTray; set => SetProperty(ref _MinimizeToTray, value); }
		bool _MinimizeToTray;

		[DefaultValue(true)]
		public bool MinimizeOnClose { get => _MinimizeOnClose; set => SetProperty(ref _MinimizeOnClose, value); }
		bool _MinimizeOnClose;

		[DefaultValue(WindowState.Normal)]
		public WindowState StartWithWindowsState { get => _StartWithWindowsState; set => SetProperty(ref _StartWithWindowsState, value); }
		WindowState _StartWithWindowsState;

		#endregion

		#region ■ Helper Functions

		public static SortableBindingList<PluginItem> RefreshPlugins(IList<PluginItem> old)
		{
			var list = new SortableBindingList<PluginItem>();
			foreach (var plugin in PluginsManager.GetPluginFunctions())
			{
				var oldItem = old?.FirstOrDefault(x => x.Id == plugin.Id);
				// Only the enable property can be modified by the user at the moment.
				if (oldItem == null)
				{
					// Enable up to medium-risk level plugins by default.
					// The default plugin risk level on the task is low.
					plugin.IsEnabled = plugin.RiskLevel >= RiskLevel.None && plugin.RiskLevel <= RiskLevel.Medium;
				}
				else
				{
					plugin.IsEnabled = oldItem.IsEnabled;
				}
				list.Add(plugin);
			}
			return list;
		}

		#endregion

	}
}
