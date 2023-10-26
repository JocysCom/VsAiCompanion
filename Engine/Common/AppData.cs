using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.ClassLibrary.Controls;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace JocysCom.VS.AiCompanion.Engine
{
	/// <summary>
	/// Application settings.
	/// </summary>
	/// <remarks>Advice: Organize all settings as flat data tables instead of using a tree structure to improve compatibility and ease conversion.</remarks>
	public class AppData : JocysCom.ClassLibrary.Configuration.ISettingsItem, INotifyPropertyChanged, ITrayManagerSettings
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

		#region Positions and Locations

		public PositionSettings StartPosition
		{
			get => _StartPosition = _StartPosition ?? new PositionSettings() { Width = 900, Height = 800 };
			set => _StartPosition = value;
		}
		private PositionSettings _StartPosition;

		public AiServiceSettings AiServiceData
		{
			get => _AiServiceData = _AiServiceData ?? new AiServiceSettings();
			set => SetProperty(ref _AiServiceData, value);
		}
		private AiServiceSettings _AiServiceData;

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

		public TaskSettings GetTaskSettings(ItemType type)
		{
			switch (type)
			{
				case ItemType.Task: return TaskData;
				case ItemType.Template: return TemplateData;
				case ItemType.FineTune: return FineTuningData;
				default: return new TaskSettings();
			}
		}

		#endregion

		#region  Spell Check

		/// <summary>Allow only one standalone copy.</summary>
		[DefaultValue(true)]
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

		public bool Enabled { get; set; }

		public bool IsEmpty =>
			false;

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

		public static Guid OpenAiServiceId
			=> AppHelper.GetGuid(nameof(AiService), OpenAiName);
		// Must be string constant or OpenAiServiceId property will get empty string.
		public const string OpenAiName = "Open AI";

		public static SortableBindingList<AiService> GetDefaultAiServices()
		{
			var openAiServiceId = OpenAiServiceId;
			var list = new SortableBindingList<AiService>
			{
				// Add open AI Model
				new AiService()
				{
					Id = openAiServiceId,
					Name = OpenAiName,
					DefaultAiModel = "gpt-3.5-turbo-16k",
					IsDefault = true,
					BaseUrl = "https://api.openai.com/v1/",
					ModelFilter = "gpt|text-davinci-[0-9+]",
				},
				// Add GPT4All Service
				new AiService()
				{
					Id = AppHelper.GetGuid(nameof(AiService), "GPT4All (Local Machine)"),
					Name = "GPT4All (Local Machine)",
					AiModels = new string[0],
					DefaultAiModel = "GPT4All Falcon",
					BaseUrl = "http://localhost:4891/v1/",
					ModelFilter = "",
				},
				// Add GPT4All Service
				new AiService()
				{
					Id = AppHelper.GetGuid(nameof(AiService), "LM Studio (Local Machine)"),
					Name = "LM Studio (Local Machine)",
					AiModels = new string[0],
					DefaultAiModel = "Mistral",
					BaseUrl = "http://localhost:1234/v1/",
					ModelFilter = "",
				},
				//// Add LocalGPT Service. Currently incompatible with OpenAI API.
				//new AiService()
				//{
				//	Id = AppHelper.GetGuid(nameof(AiService), "LocalGPT (Local Machine)"),
				//	Name = "LocalGPT (Local Machine)",
				//	AiModels = new string[0],
				//	DefaultAiModel = "llama-2-7b-chat.ggmlv3.q4_0.bin",
				//	BaseUrl = "https://localhost:5110/v1/",
				//	ModelFilter = "",
				//},
				// Add Open AI (on-premises)
				new AiService()
				{
					Id = AppHelper.GetGuid(nameof(AiService), "Open AI (On-Premises)"),
					Name = "Open AI (On-Premises)",
					AiModels = new string[0],
					DefaultAiModel = "gpt-3.5-turbo-16k",
					BaseUrl = "https://ai.company.local/v1/",
					ModelFilter = "",
				},
				// Add Azure Open AI
				new AiService()
				{
					Id = AppHelper.GetGuid(nameof(AiService), "Azure Open AI"),
					Name = "Azure Open AI",
					AiModels = new string[0],
					DefaultAiModel = "gpt-3.5-turbo-16k",
					BaseUrl = "https://api.cognitive.microsoft.com/v1/",
					ModelFilter = "",
					IsAzureOpenAI = true,
				}
			};

			return list;
		}

		public static SortableBindingList<AiModel> GetDefaultOpenAiModels()
		{
			var openAiServiceId = OpenAiServiceId;
			var list = new SortableBindingList<AiModel>();
			var names = new string[] {
				"text-davinci-003",
				"text-davinci-002",
				"text-davinci-001",
				"gpt-3.5-turbo-16k-0613",
				"gpt-3.5-turbo-16k",
				"gpt-3.5-turbo-0613",
				"gpt-3.5-turbo-0301",
				"gpt-3.5-turbo"
			};
			foreach (var name in names)
			{
				var item = new AiModel()
				{
					Id = AppHelper.GetGuid(nameof(AiModel), name),
					Name = name,
					AiServiceId = openAiServiceId,
				};
				list.Add(item);
			}
			return list;
		}

		#endregion

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void SetProperty<T>(ref T property, T value, [CallerMemberName] string propertyName = null)
		{
			property = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion

	}
}
