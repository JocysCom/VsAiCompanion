﻿using JocysCom.VS.AiCompanion.DataClient.Common;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using JocysCom.VS.AiCompanion.Engine.Controls.Chat;
using JocysCom.VS.AiCompanion.Engine.Settings;
using JocysCom.VS.AiCompanion.Plugins.Core;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class TemplateItem : AiFileListItem, ITemplateItemNames
	{
		public TemplateItem()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
			CancellationTokenSources = new BindingList<CancellationTokenSource>();
			CancellationTokenSources.ListChanged += HttpClients_ListChanged;
			PropertyChanged += TemplateItem_PropertyChanged;
		}

		private void TemplateItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(Name) || e.PropertyName == nameof(AiModel) || e.PropertyName == nameof(AiServiceId))
			{
				OnPropertyChanged(nameof(DisplayName));
			}
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

		[XmlIgnore, JsonIgnore]
		public string DisplayName { get => $"{Name} ({AiService?.Name}, {AiModel})"; }

		public string TemplateName { get => _TemplateName; set => SetProperty(ref _TemplateName, value); }
		string _TemplateName;

		/// <summary>Instructions that will be included at the start of every message.</summary>
		[DefaultValue("")]
		public string TextInstructions
		{
			get => _TextInstructions;
			set => SetProperty(ref _TextInstructions, AppHelper.ReplaceInvalidXmlChars(value));
		}
		string _TextInstructions;

		[DefaultValue(false)]
		public bool InstructionsPathEnabled { get => _InstructionsPathEnabled; set => SetProperty(ref _InstructionsPathEnabled, value); }
		bool _InstructionsPathEnabled;


		[DefaultValue(null)]
		public string InstructionsPath { get => _InstructionsPath; set => SetProperty(ref _InstructionsPath, value); }
		string _InstructionsPath;

		/// <summary>Indicates whether the property should be serialized with the XML serializer.</summary>
		public bool ShouldSerializeInstructionsPath() => !string.IsNullOrEmpty(InstructionsPath);

		#region Additional Request Data

		[DefaultValue(null)]
		public ListInfo RequestHeaders {
			get => _RequestHeaders = _RequestHeaders ?? new ListInfo();
			set => SetProperty(ref _RequestHeaders, value); }
		ListInfo _RequestHeaders;

		/// <summary>Indicates whether the property should be serialized with the XML serializer.</summary>
		public bool ShouldSerializeRequestHeaders() => _RequestHeaders?.IsEmpty == false;


		[DefaultValue(null)]
		public ListInfo RequestContentHeaders {
			get => _RequestContentHeaders = _RequestContentHeaders ?? new ListInfo();
			set => SetProperty(ref _RequestContentHeaders, value); }
		ListInfo _RequestContentHeaders;

		/// <summary>Indicates whether the property should be serialized with the XML serializer.</summary>
		public bool ShouldSerializeRequestContentHeaders() => _RequestContentHeaders?.IsEmpty == false;


		[DefaultValue(null)]
		public ListInfo RequestBodyData {
			get => _RequestBodyData = _RequestBodyData ?? new ListInfo();
			set => SetProperty(ref _RequestBodyData, value); }
		ListInfo _RequestBodyData;

		/// <summary>Indicates whether the property should be serialized with the XML serializer.</summary>
		public bool ShouldSerializeRequestBodyData() => _RequestBodyData?.IsEmpty == false;


		[DefaultValue(null)]
		public ListInfo RequestQueryData
		{
			get => _RequestQueryData = _RequestQueryData ?? new ListInfo();
			set => SetProperty(ref _RequestQueryData, value);
		}
		ListInfo _RequestQueryData;

		/// <summary>Indicates whether the property should be serialized with the XML serializer.</summary>
		public bool ShouldSerializeRequestQueryData() => _RequestQueryData?.IsEmpty == false;


		#endregion


		/// <summary>Show RISEN textboxes</summary>
		[DefaultValue(false)]
		public bool ShowRisen { get => _ShowRisen; set => SetProperty(ref _ShowRisen, value); }
		bool _ShowRisen;

		/// <summary>If true then instructions will be sent as System message, otherwise will be added the the used message.</summary>
		[DefaultValue(false)]
		public bool IsSystemInstructions { get => _IsSystemInstructions; set => SetProperty(ref _IsSystemInstructions, value); }
		bool _IsSystemInstructions;

		public bool UseSystemInstructions
			=> IsSystemInstructions && _AiModelItem.HasFeature(AiModelFeatures.SystemMessages);

		[DefaultValue("")]
		public string Text
		{
			get => _Text ?? "";
			set => SetProperty(ref _Text, AppHelper.ReplaceInvalidXmlChars(value));
		}
		string _Text;

		[DefaultValue(null)]
		public string TextPlaceholder
		{
			get => _TextPlaceholder ?? Engine.Resources.MainResources.main_Text_Placeholder;
			set => SetProperty(ref _TextPlaceholder, AppHelper.ReplaceInvalidXmlChars(value));
		}
		string _TextPlaceholder;

		/// <summary>Indicates whether the property should be serialized with the XML serializer.</summary>
		public bool ShouldSerializeTextPlaceholder() => !string.IsNullOrEmpty(TextPlaceholder) && TextPlaceholder != Engine.Resources.MainResources.main_Text_Placeholder;

		/// <summary>Attachments to send.</summary>
		[DefaultValue(null)]
		public BindingList<MessageAttachments> Attachments
		{
			get => _Attachments = _Attachments ?? new BindingList<MessageAttachments>();
			set => SetProperty(ref _Attachments, value);
		}
		BindingList<MessageAttachments> _Attachments;

		/// <summary>0 - Precise, 1 - Normal,  2 - Creative.</summary>
		[DefaultValue(1.0)]
		public double Creativity { get => _Creativity; set => SetProperty(ref _Creativity, value); }
		double _Creativity = 1.0;

		/// <summary>
		/// The maximum number of tokens that can be generated in a completion includes both displayed output tokens and reasoning tokens. This can help manage costs.
		/// </summary>
		[DefaultValue(16384)]
		public int MaxCompletionTokens { get => _MaxCompletionTokens; set => SetProperty(ref _MaxCompletionTokens, value); }
		int _MaxCompletionTokens;

		[DefaultValue(false)]
		public bool MaxCompletionTokensEnabled { get => _MaxCompletionTokensEnabled; set => SetProperty(ref _MaxCompletionTokensEnabled, value); }
		bool _MaxCompletionTokensEnabled;

		[DefaultValue(reasoning_effort.medium)]
		public reasoning_effort ReasoningEffort { get => _ReasoningEffort; set => SetProperty(ref _ReasoningEffort, value); }
		reasoning_effort _ReasoningEffort;

		/// <summary>
		/// Documents files, selction, clipboard.
		/// </summary>
		[DefaultValue(ContextType.None)]
		public ContextType AttachContext
		{
			get => _AttachContext;
			set
			{
				var oldChatValue = _AttachContext.HasFlag(ContextType.ChatHistory);
				var newChatValue = value.HasFlag(ContextType.ChatHistory);
				SetProperty(ref _AttachContext, value);
				if (oldChatValue != newChatValue)
					OnPropertyChanged(nameof(SendChatHistory));
			}

		}
		ContextType _AttachContext;

		[DefaultValue(null)]
		public ChatSettings Settings { get => _Settings; set => SetProperty(ref _Settings, value); }
		ChatSettings _Settings;

		public bool ShouldSerializeSettings() => Settings?.IsEmpty == false;

		public BindingList<MessageItem> Messages
		{
			get => _Messages = _Messages ?? new BindingList<MessageItem>();
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

		[XmlIgnore, JsonIgnore]
		public Task GenerateTitleTask;

		[XmlIgnore, JsonIgnore]
		public Task GenerateIconTask;

		[DefaultValue(SettingsSourceManager.TemplateGenerateTitleTaskName)]
		public string GenerateTitleTemplate { get => _GenerateTitleTemplate; set => SetProperty(ref _GenerateTitleTemplate, value); }
		string _GenerateTitleTemplate;

		/// <summary>Indicates whether the property should be serialized with the XML serializer.</summary>
		public bool ShouldSerializeGenerateTitleTemplate() =>
			!string.IsNullOrEmpty(GenerateTitleTemplate) &&
			GenerateTitleTemplate != SettingsSourceManager.TemplateGenerateTitleTaskName;

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

		/// <summary>
		/// Can be used used for categorized search.
		/// </summary>
		[DefaultValue("")]
		public string Tags { get => _Tags; set => SetProperty(ref _Tags, value); }
		string _Tags;

		/// <summary>Show Avatar</summary>
		[DefaultValue(false)]
		public bool ShowAvatar { get => _ShowAvatar; set => SetProperty(ref _ShowAvatar, value); }
		bool _ShowAvatar;

		/// <summary>Use avatar voice</summary>
		[DefaultValue(false)]
		public bool UseAvatarVoice { get => _UseAvatarVoice; set => SetProperty(ref _UseAvatarVoice, value); }
		bool _UseAvatarVoice;

		/// <summary>Show Avatar</summary>
		[DefaultValue(false)]
		public bool UseMonotypeFont { get => _UseMonotypeFont; set => SetProperty(ref _UseMonotypeFont, value); }
		bool _UseMonotypeFont;

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


		#region ToolChoiceRequired

		/// <summary>Specifies whether a tool function must be called by the AI model.</summary>
		[DefaultValue(false)]
		public bool ToolChoiceRequired { get => _ToolChoiceRequired; set => SetProperty(ref _ToolChoiceRequired, value); }
		bool _ToolChoiceRequired;

		/// <summary>Specifies the names of the required tool functions the AI model must call.</summary>
		[DefaultValue(null)]
		public List<string> ToolChoiceRequiredNames
		{
			get => _ToolChoiceRequiredNames = _ToolChoiceRequiredNames ?? new List<string>();
			set => SetProperty(ref _ToolChoiceRequiredNames, value);
		}
		List<string> _ToolChoiceRequiredNames;

		#endregion

		#region ToolExcludeAllExcept

		/// <summary>Specifies whether to exclude all tools except the selected ones.</summary>
		[DefaultValue(false)]
		public bool ToolExcludeAllExcept { get => _ToolExcludeAllExcept; set => SetProperty(ref _ToolExcludeAllExcept, value); }
		bool _ToolExcludeAllExcept;

		/// <summary>Specifies the names of tools to include when excluding all others.</summary>
		[DefaultValue(null)]
		public List<string> ToolExcludeAllExceptNames
		{
			get => _ToolExcludeAllExceptNames = _ToolExcludeAllExceptNames ?? new List<string>();
			set => SetProperty(ref _ToolExcludeAllExceptNames, value);
		}
		List<string> _ToolExcludeAllExceptNames;

		#endregion

		/// <summary>Indicates whether the property should be serialized with the XML serializer.</summary>
		public bool ShouldSerializeToolExcludeAllExceptNames() => _ToolExcludeAllExceptNames?.Count > 0;



		/// <summary>Indicates whether the property should be serialized with the XML serializer.</summary>
		public bool ShouldSerializeToolChoiceRequiredNames() => _ToolChoiceRequiredNames?.Count > 0;

		[DefaultValue(RiskLevel.Low)]
		public RiskLevel MaxRiskLevel { get => _MaxRiskLevel; set => SetProperty(ref _MaxRiskLevel, value); }
		RiskLevel _MaxRiskLevel;

		[DefaultValue(ToolCallApprovalProcess.User)]
		public ToolCallApprovalProcess PluginApprovalProcess { get => _PluginApprovalProcess; set => SetProperty(ref _PluginApprovalProcess, value); }
		ToolCallApprovalProcess _PluginApprovalProcess;

		[DefaultValue(SettingsSourceManager.TemplatePluginApprovalTaskName)]
		public string PluginApprovalTemplate { get => _PluginApprovalTemplate; set => SetProperty(ref _PluginApprovalTemplate, value); }
		string _PluginApprovalTemplate;

		[DefaultValue(null)]
		public string Context0ListName { get => _Context0ListName; set => SetProperty(ref _Context0ListName, value); }
		string _Context0ListName;

		[DefaultValue(null)]
		public string Context1ListName { get => _Context1ListName; set => SetProperty(ref _Context1ListName, value); }
		string _Context1ListName;

		[DefaultValue(null)]
		public string Context2ListName { get => _Context2ListName; set => SetProperty(ref _Context2ListName, value); }
		string _Context2ListName;

		[DefaultValue(null)]
		public string Context3ListName { get => _Context3ListName; set => SetProperty(ref _Context3ListName, value); }
		string _Context3ListName;

		[DefaultValue(null)]
		public string Context4ListName { get => _Context4ListName; set => SetProperty(ref _Context4ListName, value); }
		string _Context4ListName;

		[DefaultValue(null)]
		public string Context5ListName { get => _Context5ListName; set => SetProperty(ref _Context5ListName, value); }
		string _Context5ListName;

		[DefaultValue(null)]
		public string Context6ListName { get => _Context6ListName; set => SetProperty(ref _Context6ListName, value); }
		string _Context6ListName;

		[DefaultValue(null)]
		public string Context7ListName { get => _Context7ListName; set => SetProperty(ref _Context7ListName, value); }
		string _Context7ListName;

		[DefaultValue(null)]
		public string Context8ListName { get => _Context8ListName; set => SetProperty(ref _Context8ListName, value); }
		string _Context8ListName;

		[DefaultValue(message_role.system)]
		public message_role ContextListRole { get => _ContextListRole; set => SetProperty(ref _ContextListRole, value); }
		message_role _ContextListRole;

		[XmlIgnore, JsonIgnore]
		public BindingList<PluginApprovalItem> PluginFunctionCalls { get; set; } = new BindingList<PluginApprovalItem>();

		#endregion

		[XmlIgnore, JsonIgnore]
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


		[XmlIgnore, JsonIgnore]
		public object Tag;

		public TemplateItem Copy(bool newId, bool copyTempMessages = false)
		{
			var copy = new TemplateItem();
			AppHelper.CopyProperties(this, copy);
			copy.TemplateName = Name; // Will be used to find original template.
			copy.Messages = new BindingList<MessageItem>();
			var messages = Messages
				.Where(x => copyTempMessages || !x.IsTemp)
				.Select(x => x.Copy(newId))
				.ToList();
			foreach (var message in messages)
				copy.Messages.Add(message);
			return copy;
		}

		#region Canvaas Panel

		[DefaultValue(false)]
		public bool CanvasPanelEnabled
		{
			get => _CanvasPanelEnabled;
			set => SetProperty(ref _CanvasPanelEnabled, value);
		}
		bool _CanvasPanelEnabled;

		[DefaultValue(null)]
		public string CanvasEditorElementPath
		{
			get => _CanvasEditorElementPath;
			set => SetProperty(ref _CanvasEditorElementPath, value);
		}
		string _CanvasEditorElementPath;

		#endregion

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

		[DefaultValue("Prompts")]
		public string ListPromptName { get => _ListPromptName; set => SetProperty(ref _ListPromptName, value); }
		string _ListPromptName;

		[DefaultValue("Info Known")]
		public string ListPromptOption { get => _ListPromptOption; set => SetProperty(ref _ListPromptOption, value); }
		string _ListPromptOption;

		#endregion

		#region Embeddings

		[DefaultValue(false)]
		public bool UseEmbeddings { get => _UseEmbeddings; set => SetProperty(ref _UseEmbeddings, value); }
		bool _UseEmbeddings;

		[DefaultValue("")]
		public string EmbeddingName { get => _EmbeddingName; set => SetProperty(ref _EmbeddingName, value); }
		string _EmbeddingName;

		[DefaultValue("")]
		public string EmbeddingGroupName { get => _EmbeddingGroupName; set => SetProperty(ref _EmbeddingGroupName, value); }
		string _EmbeddingGroupName;

		[DefaultValue(EmbeddingGroupFlag.None)]
		public EmbeddingGroupFlag EmbeddingGroupFlag { get => _EmbeddingGroupFlag; set => SetProperty(ref _EmbeddingGroupFlag, value); }
		EmbeddingGroupFlag _EmbeddingGroupFlag;

		#endregion

		#region Selections

		public List<string> AttachmentsSelection { get => _AttachmentsDataSelection; set => SetProperty(ref _AttachmentsDataSelection, value); }
		List<string> _AttachmentsDataSelection;

		[DefaultValue(null)]
		public List<TextBoxData> UiSelections { get => _UiSelections; set => SetProperty(ref _UiSelections, value); }
		List<TextBoxData> _UiSelections;

		/// <summary>Indicates whether the property should be serialized with the XML serializer.</summary>
		public bool ShouldSerializeUiSelections() => _UiSelections?.Count > 0;

		#endregion

		#region AI Mail Client

		[DefaultValue(false)]
		public bool UseMailAccount { get => _UseMailAccount; set => SetProperty(ref _UseMailAccount, value); }
		bool _UseMailAccount;

		[DefaultValue(false)]
		public bool EnableCopilotInstructions { get => _EnableCopilotInstructionst; set => SetProperty(ref _EnableCopilotInstructionst, value); }
		bool _EnableCopilotInstructionst;

		/// <summary>
		/// Inbox monitoring is enabled.
		/// </summary>
		[DefaultValue(false)]
		public bool MonitorInbox
		{
			get => _MonitorInbox;
			set
			{
				SetProperty(ref _MonitorInbox, value);
				_ = AiMailClient.MonitorMailbox(value);
			}

		}
		bool _MonitorInbox;

		/// <summary>
		/// Can be used used for categorized search.
		/// </summary>
		[DefaultValue("")]
		public string MailAccount
		{
			get => _MailAccount;
			set
			{
				SetProperty(ref _MailAccount, value);
				UpdateMailClientAccount();
			}

		}
		string _MailAccount;

		[XmlIgnore, JsonIgnore]
		public AiMailClient AiMailClient
		{
			get
			{
				if (_AiMailClient == null)
				{
					_AiMailClient = new AiMailClient();
					_AiMailClient.NewMessage += _AiMailClient_NewMessage;
					UpdateMailClientAccount();
				}
				return _AiMailClient;
			}
		}
		AiMailClient _AiMailClient;


		private async void _AiMailClient_NewMessage(object sender, MimeKit.MimeMessage e)
		{
			// Wait for the oportunity to paste message.
			while (IsBusy)
				await Task.Delay(500);
			var ms = new MemoryStream();
			e.WriteTo(ms);
			var attachment = new MessageAttachments()
			{
				Title = "Mail Message",
				Instructions = "Analyse and reply to this Email Message (*.eml)",
				Type = ContextType.None,
				Data = e.ToString(),
			};
			var userMessage = new MessageItem();
			userMessage.Type = MessageType.Out;
			userMessage.Body = "You've got a new email message.";
			userMessage.Attachments.Add(attachment);
			_ = Global.MainControl.Dispatcher.Invoke(async () =>
			{
				Messages.Add(userMessage);
				await Companions.ClientHelper.Send(this, overrideMessage: userMessage);
			});
		}

		public void UpdateMailClientAccount()
		{
			var account = Global.AppSettings.MailAccounts.FirstOrDefault(x => x.Name.Equals(MailAccount));
			if (AiMailClient.Account != account)
				AiMailClient.Account = account;
		}

		#endregion

		#region External AI Models

		[DefaultValue(SettingsSourceManager.TemplatePlugin_Model_TextToAudio)]
		public string TemplateTextToAudio { get => _TemplateTextToAudio; set => SetProperty(ref _TemplateTextToAudio, value); }
		string _TemplateTextToAudio;

		[DefaultValue(SettingsSourceManager.TemplatePlugin_Model_AudioToText)]
		public string TemplateAudioToText { get => _TemplateAudioToText; set => SetProperty(ref _TemplateAudioToText, value); }
		string _TemplateAudioToText;

		[DefaultValue(SettingsSourceManager.TemplatePlugin_Model_VideoToText)]
		public string TemplateVideoToText { get => _TemplateVideoToText; set => SetProperty(ref _TemplateVideoToText, value); }
		string _TemplateVideoToText;

		[DefaultValue(SettingsSourceManager.TemplatePlugin_Model_GenerateImage)]
		public string TemplateCreateImage { get => _TemplateCreateImage; set => SetProperty(ref _TemplateCreateImage, value); }
		string _TemplateCreateImage;

		[DefaultValue(SettingsSourceManager.TemplatePlugin_Model_ModifyImage)]
		public string TemplateModifyImage { get => _TemplateModifyImage; set => SetProperty(ref _TemplateModifyImage, value); }
		string _TemplateModifyImage;


		[DefaultValue(SettingsSourceManager.TemplatePlugin_Model_TextToVideo)]
		public string TemplateTextToVideo { get => _TemplateTextToVideo; set => SetProperty(ref _TemplateTextToVideo, value); }
		string _TemplateTextToVideo;

		#endregion

	}
}
