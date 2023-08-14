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
			get => _WindowPosition = _WindowPosition ?? new PositionSettings();
			set => _WindowPosition = value;
		}
		private PositionSettings _WindowPosition;

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

		public TaskSettings GetTaskSettings(ItemType type)
		{
			switch (type)
			{
				case ItemType.Task: return TaskData;
				case ItemType.Template: return TemplateData;
				default: return new TaskSettings();
			}
		}

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

		public static Guid OpenAiId { get; } = AppHelper.GetGuid(nameof(AiService), OpenAiName);
		public static string OpenAiName { get; } = "Open AI";

		public static SortableBindingList<AiService> GetDefaultAiServices()
		{
			var list = new SortableBindingList<AiService>();
			// Add open AI Model
			var s1 = new AiService()
			{
				Id = OpenAiId,
				Name = OpenAiName,
				DefaultAiModel = "gpt-3.5-turbo-16k",
				IsDefault = true,
				BaseUrl = "https://api.openai.com/v1/",
				ModelFilter = "gpt|text-davinci-[0-9+]",
			};
			list.Add(s1);
			// Add GPT4All Service
			var s2 = new AiService()
			{
				Id = AppHelper.GetGuid(nameof(AiService), "GPT4All (Local Machine)"),
				Name = "GPT4All (Local Machine)",
				AiModels = new string[0],
				DefaultAiModel = "GPT4All Falcon",
				BaseUrl = "https://localhost:4891/v1/",
				ModelFilter = "",
			};
			list.Add(s2);
			// Add Open AI (on-premises)
			var s3 = new AiService()
			{
				Id = AppHelper.GetGuid(nameof(AiService), "Open AI (On-Premises)"),
				Name = "Open AI (On-Premises)",
				AiModels = new string[0],
				DefaultAiModel = "gpt-3.5-turbo-16k",
				BaseUrl = "https://ai.company.local/v1/",
				ModelFilter = "",
			};
			list.Add(s3);
			// Add Azure Open AI
			var s4 = new AiService()
			{
				Id = AppHelper.GetGuid(nameof(AiService), "Azure Open AI"),
				Name = "Azure Open AI",
				AiModels = new string[0],
				DefaultAiModel = "gpt-3.5-turbo-16k",
				BaseUrl = "https://api.cognitive.microsoft.com/v1/",
				ModelFilter = "",
			};
			list.Add(s4);
			return list;
		}

		public static SortableBindingList<AiModel> GetDefaultAiModels()
		{
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
					AiServiceId = OpenAiId,
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
