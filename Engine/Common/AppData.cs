using JocysCom.ClassLibrary.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace JocysCom.VS.AiCompanion.Engine
{
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

		[DefaultValue(0.3)]
		public double TasksGridSplitterPosition { get => _TasksGridSplitterPosition; set => SetProperty(ref _TasksGridSplitterPosition, value); }
		private double _TasksGridSplitterPosition;

		[DefaultValue(0.3)]
		public double TemplatesGridSplitterPosition { get => _TemplatesGridSplitterPosition; set => SetProperty(ref _TemplatesGridSplitterPosition, value); }
		private double _TemplatesGridSplitterPosition;

		public TaskSettings TaskData
		{
			get => _TaskData = _TaskData ?? new TaskSettings();
			set => SetProperty(ref _TaskData, value);
		}
		private TaskSettings _TaskData = new TaskSettings();

		public TaskSettings TemplateData
		{
			get => _TemplateData = _TemplateData ?? new TaskSettings();
			set => SetProperty(ref _TemplateData, value);
		}
		private TaskSettings _TemplateData = new TaskSettings();

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

		#endregion

		public Companions.ChatGPT.Settings OpenAiSettings
		{
			get => _OpenAiSettings = _OpenAiSettings ?? new Companions.ChatGPT.Settings();
			set => _OpenAiSettings = value;
		}
		private Companions.ChatGPT.Settings _OpenAiSettings;

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
