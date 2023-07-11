using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls.Chat;
using System;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class TemplateItem : ISettingsItem, INotifyPropertyChanged, ISettingsItemFile
	{
		public TemplateItem()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
			_AiModel = Companions.ChatGPT.Settings.AiModelDefault;
		}

		public string Name { get => _Name; set => SetProperty(ref _Name, value); }
		string _Name;

		public string TemplateName { get => _TemplateName; set => SetProperty(ref _TemplateName, value); }
		string _TemplateName;

		public string AiModel { get => _AiModel; set => SetProperty(ref _AiModel, value); }
		string _AiModel;

		public string IconType { get => _IconType; set => SetProperty(ref _IconType, value); }
		string _IconType;

		/// <summary>Instructions that will be included at the start of every message.</summary>
		[DefaultValue("")]
		public string TextInstructions { get => _TextInstructions; set => SetProperty(ref _TextInstructions, value); }
		string _TextInstructions;

		/// <summary>Show Instructions</summary>
		[DefaultValue(true)]
		public bool ShowInstructions { get => _ShowInstructions; set => SetProperty(ref _ShowInstructions, value); }
		bool _ShowInstructions;

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
		[DefaultValue(AttachmentType.None)]
		public AttachmentType AttachContext { get => _AttachContext; set => SetProperty(ref _AttachContext, value); }
		AttachmentType _AttachContext;

		/// <summary>
		/// Chat history with AI.
		/// </summary>
		[DefaultValue(true)]
		public bool AttachChatHistory { get => _AttachChatHistory; set => SetProperty(ref _AttachChatHistory, value); }
		bool _AttachChatHistory;

		public ChatSettings Settings { get => _Settings; set => SetProperty(ref _Settings, value); }
		ChatSettings _Settings;

		[XmlIgnore]
		public DrawingImage Icon { get => _Icon; }
		DrawingImage _Icon;

		public Companions.CompanionType Type { get => _Type; set => SetProperty(ref _Type, value); }
		Companions.CompanionType _Type;

		public string StatusText { get => _StatusText; set => SetProperty(ref _StatusText, value); }
		string _StatusText;

		public System.Windows.MessageBoxImage StatusCode { get => _StatusCode; set => SetProperty(ref _StatusCode, value); }
		System.Windows.MessageBoxImage _StatusCode;

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

		public bool IsPreview { get => _IsPreview; set => SetProperty(ref _IsPreview, value); }
		bool _IsPreview;

		[DefaultValue(true)]	
		public bool IsFavorite { get => _IsFavorite; set => SetProperty(ref _IsFavorite, value); }
		bool _IsFavorite;

		[DefaultValue(false)]
		public bool UseMacros { get => _UseMacros; set => SetProperty(ref _UseMacros, value); }
		bool _UseMacros;

		public bool IsChecked
		{
			get => _IsChecked;
			set => SetProperty(ref _IsChecked, value);
		}
		bool _IsChecked;

		public bool IsEnabled { get => _IsEnabled; set => SetProperty(ref _IsEnabled, value); }
		bool _IsEnabled;

		public ItemType ItemType { get => _ItemType; set => SetProperty(ref _ItemType, value); }
		ItemType _ItemType;

		[XmlIgnore]
		public object Tag;
		public string IconData { get => _IconData; set => SetProperty(ref _IconData, value); }
		string _IconData;

		public TemplateItem Copy(bool newId)
		{
			var copy = new TemplateItem();
			copy.AiModel = AiModel;
			copy.AttachChatHistory = AttachChatHistory;
			copy.AttachContext = AttachContext;
			copy.AutoFormatCode = AutoFormatCode;
			copy.AutoOperation = AutoOperation;
			copy.AutoRemove = AutoRemove;
			copy.AutoSend = AutoSend;
			copy.Creativity = Creativity;
			copy.IconData = IconData;
			copy.IconType = IconType;
			copy.IsChecked = IsChecked;
			copy.IsEnabled = IsEnabled;
			copy.IsFavorite = IsFavorite;
			copy.IsPreview = IsPreview;
			copy.UseMacros = UseMacros;
			copy.MessageBoxOperation = MessageBoxOperation;
			copy.Name = Name;
			copy.ShowInstructions = ShowInstructions;
			copy.TemplateName = Name; // Will be used tom find original template.
			copy.Text = Text;
			copy.TextInstructions = TextInstructions;
			copy.Messages = new BindingList<MessageItem>();
			var messages = Messages.Select(x => x.Copy(newId)).ToList();
			foreach (var message in messages)
				copy.Messages.Add(message);
			return copy;
		}
		public void SetIcon(string contents, string type = ".svg")
		{
			var base64 = Converters.SvgHelper.GetBase64(contents);
			IconType = type;
			IconData = base64;
		}

		#region HTTP Client

		/// <summary>Show Instructions</summary>

		[XmlIgnore, DefaultValue(false)]
		public bool IsClientBusy { get => _IsClientBusy; set => SetProperty(ref _IsClientBusy, value); }
		bool _IsClientBusy;

		[XmlIgnore, DefaultValue(null)]
		public HttpClient HttpClient { get => _HttpClient; set => SetProperty(ref _HttpClient, value); }
		HttpClient _HttpClient;

		public void StopClient()
		{
			HttpClient?.CancelPendingRequests();
		}

		#endregion

		#region ■ ISettingsItem
		bool ISettingsItem.Enabled { get => IsEnabled; set => IsEnabled = value; }

		public bool IsEmpty =>
			string.IsNullOrEmpty(Name);

		#endregion

		#region ■ ISettingsItemFile

		[XmlIgnore]
		string ISettingsItemFile.BaseName { get => Name; set => Name = value; }

		[XmlIgnore]
		DateTime ISettingsItemFile.WriteTime { get; set; }

		#endregion

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void SetProperty<T>(ref T property, T value, [CallerMemberName] string propertyName = null)
		{
			if (propertyName == nameof(IconData))
			{
				var svgContent = Converters.SvgHelper.GetContent((string)(object)value);
				_Icon = Converters.SvgHelper.LoadSvgFromString(svgContent);
				OnPropertyChanged(nameof(Icon));
			}
			property = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
			((ISettingsItemFile)this).WriteTime = DateTime.Now;
		}

		#endregion

	}
}
