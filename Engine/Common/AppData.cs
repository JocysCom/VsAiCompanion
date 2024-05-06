using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Controls.UpdateControl;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;

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

		[DefaultValue(false)]
		public bool IsEnterprise { get => _IsEnterprise; set => SetProperty(ref _IsEnterprise, value); }
		private bool _IsEnterprise;

		[DefaultValue("https://github.com/JocysCom/VsAiCompanion/raw/main/Engine/Resources/Settings.CompanyName.zip")]
		public string ConfigurationUrl { get => _ConfigurationUrl; set => SetProperty(ref _ConfigurationUrl, value); }
		private string _ConfigurationUrl;


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

		public TaskSettings AiModelData
		{
			get => _AiModelData = _AiModelData ?? new TaskSettings();
			set => SetProperty(ref _AiModelData, value);
		}
		private TaskSettings _AiModelData;

		public TaskSettings AiServiceData
		{
			get => _AiServiceData = _AiServiceData ?? new TaskSettings();
			set => SetProperty(ref _AiServiceData, value);
		}
		private TaskSettings _AiServiceData;

		public TaskSettings TaskData
		{
			get => _TaskData = _TaskData ?? new TaskSettings();
			set => SetProperty(ref _TaskData, value);
		}
		private TaskSettings _TaskData;

		public TaskSettings TemplateData
		{
			get => _TemplateData = _TemplateData ?? new TaskSettings();
			set => SetProperty(ref _TemplateData, value);
		}
		private TaskSettings _TemplateData;

		public TaskSettings FineTuningData
		{
			get => _FineTuningData = _FineTuningData ?? new TaskSettings();
			set => SetProperty(ref _FineTuningData, value);
		}
		private TaskSettings _FineTuningData;

		public TaskSettings AssistantData
		{
			get => _AssistantData = _AssistantData ?? new TaskSettings();
			set => SetProperty(ref _AssistantData, value);
		}
		private TaskSettings _AssistantData;

		public TaskSettings ListsData
		{
			get => _ListsData = _ListsData ?? new TaskSettings();
			set => SetProperty(ref _ListsData, value);
		}
		private TaskSettings _ListsData;

		public TaskSettings EmbeddingsData
		{
			get => _EmbeddingsData = _EmbeddingsData ?? new TaskSettings();
			set => SetProperty(ref _EmbeddingsData, value);
		}
		private TaskSettings _EmbeddingsData;

		public TaskSettings MailAccountData
		{
			get => _MailAccountData = _MailAccountData ?? new TaskSettings();
			set => SetProperty(ref _MailAccountData, value);
		}
		private TaskSettings _MailAccountData;

		public TaskSettings GetTaskSettings(ItemType type)
		{
			switch (type)
			{
				case ItemType.Task: return TaskData;
				case ItemType.Template: return TemplateData;
				case ItemType.FineTuning: return FineTuningData;
				case ItemType.Assistant: return AssistantData;
				case ItemType.Lists: return ListsData;
				case ItemType.Embeddings: return EmbeddingsData;
				case ItemType.MailAccount: return MailAccountData;
				default: return new TaskSettings();
			}
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

		[DefaultValue(true), Description("Enable Form Info (CTRL+SHIFT+RMB)")]
		public bool EnableShowFormInfo { get => _EnableShowFormInfo; set => SetProperty(ref _EnableShowFormInfo, value); }
		bool _EnableShowFormInfo;

		#endregion

		/// <summary>
		/// Remove models without services.
		/// </summary>
		public void CleanupAiModels()
		{
			var serviceIds = AiServices.Select(x => x.Id).ToArray();
			// Remove models without services.
			var modelsToRemove = AiModels.Where(x => !serviceIds.Contains(x.AiServiceId)).ToArray();
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
		}

		public SortableBindingList<AiService> AiServices
		{
			get
			{
				lock (_AiServicesLock)
				{
					if (_AiServices == null || _AiServices.Count == 0)
						_AiServices = new SortableBindingList<AiService>();
					return _AiServices;
				}
			}
			set => _AiServices = value;
		}
		private SortableBindingList<AiService> _AiServices;
		private object _AiServicesLock = new object();


		public SortableBindingList<MailAccount> MailAccounts
		{
			get
			{
				lock (_MailAccountsLock)
				{
					if (_MailAccounts == null || _MailAccounts.Count == 0)
						_MailAccounts = new SortableBindingList<MailAccount>();
					return _MailAccounts;
				}
			}
			set => _MailAccounts = value;
		}
		private SortableBindingList<MailAccount> _MailAccounts;
		private object _MailAccountsLock = new object();

		public SortableBindingList<PluginItem> Plugins
		{
			get
			{
				lock (_PluginsLock)
				{
					if (_Plugins == null || _Plugins.Count == 0)
						_Plugins = new SortableBindingList<PluginItem>();
					return _Plugins;
				}
			}
			set => _Plugins = value;
		}
		private SortableBindingList<PluginItem> _Plugins;
		private object _PluginsLock = new object();

		public SortableBindingList<AiModel> AiModels
		{
			get
			{
				if (_AiModels == null)
					_AiModels = new SortableBindingList<AiModel>();
				return _AiModels;
			}
			set => _AiModels = value;
		}
		private SortableBindingList<AiModel> _AiModels;

		public string MarkdownLanguageNames { get; set; } =
			"ABAP,ABNF,AL,ANTLR4,APL,AQL,ARFF,ARMASM,ASM6502,AWK,ActionScript,Ada,Agda,ApacheConf,Apex," +
			"AppleScript,Arduino,Arturo,Asciidoc,Asmatmel,Aspnet,AutoHotkey,AutoIt,AviSynth,Avro-IDL," +
			"BBCode,BBJ,BNF,BQN,BSL,Bash,Basic,Batch,Bicep,Birb,Bison,Brainfuck,BrightScript,Bro," +
			"C,CFScript,CIL,CMake,COBOL,CSHTML,CSP,CSS,CSS-Extras,CSV,Chaiscript,CilkC,CilkCpp,Clike,Clojure,CoffeeScript,Concurnas,Cooklang,Coq,Cpp,Crystal,Csharp,Cue,Cypher," +
			"D,DAX,DNS-Zone-File,Dart,DataWeave,Dhall,Diff,Django,Docker,Dot," +
			"EBNF,EJS,ERB,EditorConfig,Eiffel,Elixir,Elm,Erlang,Excel-Formula," +
			"FORTRAN,FTL,Factor,False,Firestore-Security-Rules,Flow,Fsharp," +
			"GAP,GCode,GEDCOM,GLSL,GML,GN,GdScript,Gettext,Gherkin,Git,Go,Go-Module,Gradle,Graphql,Groovy," +
			"HCL,HLSL,HPKP,HSTS,HTTP,Haml,Handlebars,Haskell,Haxe,Hoon," +
			"ICU-Message-Format,IECSt,INI,IchigoJam,Icon,Idris,Ignore,Inform7,Io," +
			"J,JEXL,JQ,JS-Extras,JS-Templates,JSDoc,JSON,JSON5,JSONP,JSStackTrace,JSX," +
			"Java,JavaDoc,JavaDocLike,JavaScript,JavaStackTrace,Jolie,Julia," +
			"KeepALIVED,Keyman,Kotlin,Kumir,Kusto," +
			"LLVM,LOLCode,Latex,Latte,Less,LilyPond,Linker-Script,Liquid,Lisp,LiveScript,Log,Lua," +
			"MAXScript,MEL,Magma,Makefile,Markdown,Markup,Markup-Templating,Mata,Matlab,Mermaid,MetaFont,Mizar,MongoDB,Monkey,MoonScript," +
			"N1QL,N4JS,NASM,NSIS,Nand2Tetris-HDL,Naniscript,Neon,Nevod,Nginx,Nim,Nix," +
			"OCaml,ObjectiveC,Odin,OpenCL,OpenQASM,Oz," +
			"PCAxis,PHP,PHP-Extras,PHPDoc,PLSQL,PSL,PariGP,Parser,Pascal,PascalIGO,PeopleCode,Perl,Plant-UML,PowerQuery," +
			"PowerShell,Processing,Prolog,PromQL,Properties,Protobuf,Pug,Puppet,Pure,PureBasic,PureScript,Python," +
			"Q,QML,QSharp,Qore," +
			"R,REST,RIP,Racket,Reason,Regex,Rego,RenPY,Rescript,Roboconf,RobotFramework,Ruby,Rust," +
			"SAS,SCSS,SML,SPARQL,SQF,SQL,Sass,Scala,Scheme,Shell-Session,Smali,SmallTalk,Smarty,Solidity," +
			"Solution-File,Soy,Splunk-SPL,Squirrel,Stan,Stata,Stylus,SuperCollider,Swift,Systemd," +
			"T4-CS,T4-Templating,T4-VB,TAP,TOML,TSX,TT2,Tcl,Textile,Tremor,Turtle,Twig,TypeScript,TypoScript," +
			"UORazor,URI,UnrealScript," +
			"V,VBNet,VHDL,Vala,Velocity,Verilog,Vim,Visual-Basic," +
			"WASM,WGSL,WarpScript,Web-IDL,Wiki,Wolfram,Wren," +
			"XML,XQuery,Xeora,Xojo," +
			"YAML,Yang,ZigetLua";


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
			foreach (var plugin in PluginsManager.PluginFunctions)
			{
				var item = new PluginItem(plugin.Value);
				var oldItem = old?.FirstOrDefault(x => x.Id == item.Id);
				// Only the enable property can be modified by the user at the moment.
				if (oldItem == null)
				{
					// Enable up to medium-risk level plugins by default.
					// The default plugin risk level on the task is low.
					item.IsEnabled = item.RiskLevel >= RiskLevel.None && item.RiskLevel <= RiskLevel.Medium;
				}
				else
				{
					item.IsEnabled = oldItem.IsEnabled;
				}
				list.Add(item);
			}
			return list;
		}

		#endregion

	}
}
