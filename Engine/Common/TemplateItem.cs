using JocysCom.VS.AiCompanion.Engine.Controls.Chat;
using JocysCom.VS.AiCompanion.Plugins.Core;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class TemplateItem : FileListItem
	{
		public TemplateItem()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
			CancellationTokenSources = new BindingList<CancellationTokenSource>();
			CancellationTokenSources.ListChanged += HttpClients_ListChanged;
		}

		private void HttpClients_ListChanged(object sender, ListChangedEventArgs e)
		{
			if (e.ListChangedType == ListChangedType.ItemAdded || e.ListChangedType == ListChangedType.ItemDeleted)
			{
				var isBusy = CancellationTokenSources.Count > 0;
				if (IsBusy != isBusy)
					IsBusy = isBusy;
			}
		}

		public string TemplateName { get => _TemplateName; set => SetProperty(ref _TemplateName, value); }
		string _TemplateName;

		/// <summary>Instructions that will be included at the start of every message.</summary>
		[DefaultValue("")]
		public string TextInstructions { get => _TextInstructions; set => SetProperty(ref _TextInstructions, value); }
		string _TextInstructions;

		/// <summary>Show Instructions</summary>
		[DefaultValue(true)]
		public bool ShowInstructions { get => _ShowInstructions; set => SetProperty(ref _ShowInstructions, value); }
		bool _ShowInstructions;

		/// <summary>If true then instructions will be sent as System message, otherwise will be added the the used message.</summary>
		[DefaultValue(false)]
		public bool IsSystemInstructions { get => _IsSystemInstructions; set => SetProperty(ref _IsSystemInstructions, value); }
		bool _IsSystemInstructions;

		[DefaultValue("")]
		public string Text { get => _Text ?? ""; set => SetProperty(ref _Text, value); }
		string _Text;

		/// <summary>0 - Precise, 1 - Normal,  2 - Creative.</summary>
		[DefaultValue(1.0)]
		public double Creativity { get => _Creativity; set => SetProperty(ref _Creativity, value); }
		double _Creativity = 1.0;

		/// <summary>
		/// Documents files, selction, clipboard.
		/// </summary>
		[DefaultValue(ContextType.None)]
		public ContextType AttachContext { get => _AttachContext; set => SetProperty(ref _AttachContext, value); }
		ContextType _AttachContext;

		public ChatSettings Settings { get => _Settings; set => SetProperty(ref _Settings, value); }
		ChatSettings _Settings;

		public BindingList<MessageItem> Messages
		{
			get
			{
				if (_Messages == null)
					_Messages = new BindingList<MessageItem>();
				return _Messages;
			}
			set => SetProperty(ref _Messages, value);
		}
		BindingList<MessageItem> _Messages;

		/// <summary>
		/// Automatically send request to AI.
		/// </summary>
		public bool AutoSend { get => _AutoSend; set => SetProperty(ref _AutoSend, value); }
		bool _AutoSend;

		/// <summary>
		/// Automatically clear message from send text box.
		/// </summary>
		public MessageBoxOperation MessageBoxOperation { get => _MessageBoxOperation; set => SetProperty(ref _MessageBoxOperation, value); }
		MessageBoxOperation _MessageBoxOperation = MessageBoxOperation.ClearMessage;

		/// <summary>
		/// Automatically perform operation on successfull results.
		/// </summary>
		public DataOperation AutoOperation { get => _AutoOperation; set => SetProperty(ref _AutoOperation, value); }
		DataOperation _AutoOperation;

		/// <summary>
		/// Automatically remove item from the task list when it completes successfully.
		/// </summary>
		public bool AutoRemove { get => _AutoRemove; set => SetProperty(ref _AutoRemove, value); }
		bool _AutoRemove;

		public bool AutoFormatCode { get => _AutoFormatCode; set => SetProperty(ref _AutoFormatCode, value); }
		bool _AutoFormatCode;

		[XmlIgnore]
		public Task GenerateTitleTask;

		[DefaultValue(false)]
		public bool AutoGenerateTitle { get => _AutoGenerateTitle; set => SetProperty(ref _AutoGenerateTitle, value); }
		bool _AutoGenerateTitle;

		[DefaultValue(false)]
		public bool AutoFormatMessage { get => _AutoFormatMessage; set => SetProperty(ref _AutoFormatMessage, value); }
		bool _AutoFormatMessage;

		public bool IsPreview { get => _IsPreview; set => SetProperty(ref _IsPreview, value); }
		bool _IsPreview;

		[DefaultValue(true)]
		public bool IsFavorite { get => _IsFavorite; set => SetProperty(ref _IsFavorite, value); }
		bool _IsFavorite;

		[DefaultValue(false)]
		public bool UseMacros { get => _UseMacros; set => SetProperty(ref _UseMacros, value); }
		bool _UseMacros;

		[DefaultValue(false)]
		public bool UseMaximumContext { get => _UseMaximumContext; set => SetProperty(ref _UseMaximumContext, value); }
		bool _UseMaximumContext;

		public ItemType ItemType { get => _ItemType; set => SetProperty(ref _ItemType, value); }
		ItemType _ItemType;

		#region Plugins

		[DefaultValue(false)]
		public bool PluginsEnabled
		{
			get => _PluginsEnabled;
			set
			{
				SetProperty(ref _PluginsEnabled, value);
				// Plugins require chat history.
				if (value && !SendChatHistory)
					SendChatHistory = true;
			}
		}
		bool _PluginsEnabled;

		[DefaultValue(RiskLevel.Low)]
		public RiskLevel MaxRiskLevel { get => _MaxRiskLevel; set => SetProperty(ref _MaxRiskLevel, value); }
		RiskLevel _MaxRiskLevel;

		[DefaultValue(ToolCallApprovalProcess.User)]
		public ToolCallApprovalProcess PluginApprovalProcess { get => _PluginApprovalProcess; set => SetProperty(ref _PluginApprovalProcess, value); }
		ToolCallApprovalProcess _PluginApprovalProcess;

		[DefaultValue(Companions.ClientHelper.PluginApprovalTaskName)]
		public string PluginApprovalTemplate { get => _PluginApprovalTemplate; set => SetProperty(ref _PluginApprovalTemplate, value); }
		string _PluginApprovalTemplate;


		[XmlIgnore]
		public BindingList<PluginApprovalItem> PluginFunctionCalls { get; set; } = new BindingList<PluginApprovalItem>();

		#endregion

		[XmlIgnore]
		public bool SendChatHistory
		{
			get => AttachContext.HasFlag(ContextType.ChatHistory);
			set
			{
				AttachContext = value
					? AttachContext |= ContextType.ChatHistory
					: AttachContext &= ~ContextType.ChatHistory;
				SetProperty(ref _SendChatHistory, value);
			}
		}
		bool _SendChatHistory;


		[XmlIgnore]
		public object Tag;

		public TemplateItem Copy(bool newId)
		{
			var copy = new TemplateItem();
			AppHelper.CopyProperties(this, copy);
			copy.TemplateName = Name; // Will be used to find original template.
			copy.Messages = new BindingList<MessageItem>();
			var messages = Messages.Select(x => x.Copy(newId)).ToList();
			foreach (var message in messages)
				copy.Messages.Add(message);
			return copy;
		}

		#region Markdown Languages

		[DefaultValue("JSON")]
		public string MarkdownLanguageName { get => _MarkdownLanguageName; set => SetProperty(ref _MarkdownLanguageName, value); }
		string _MarkdownLanguageName;

		#endregion

		#region Prompting

		/// <summary>Show Prompting</summary>
		[DefaultValue(false)]
		public bool ShowPrompting { get => _ShowPrompting; set => SetProperty(ref _ShowPrompting, value); }
		bool _ShowPrompting;

		[DefaultValue("Role")]
		public string PromptName { get => _PromptName; set => SetProperty(ref _PromptName, value); }
		string _PromptName;

		[DefaultValue("helpful assistant")]
		public string PromptOption { get => _PromptOption; set => SetProperty(ref _PromptOption, value); }
		string _PromptOption;

		#endregion

		public BindingList<string> Attachments { get => _Attachments; set => SetProperty(ref _Attachments, value); }
		BindingList<string> _Attachments;

		#region Selections

		public List<string> AttachmentsSelection { get => _AttachmentsDataSelection; set => SetProperty(ref _AttachmentsDataSelection, value); }
		List<string> _AttachmentsDataSelection;

		#endregion


	}
}
