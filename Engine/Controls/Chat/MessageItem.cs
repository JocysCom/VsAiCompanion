using JocysCom.ClassLibrary.Configuration;
using JocysCom.VS.AiCompanion.Engine.Companions;
using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Chat
{
	public class MessageItem : SettingsItem
	{
		public MessageItem()
		{
			_Id = Guid.NewGuid().ToString("N");
			Date = DateTime.Now;
		}

		public MessageItem(string user, string body, MessageType type = MessageType.Information)
		{
			_Id = Guid.NewGuid().ToString("N");
			_Body = body;
			_User = user;
			_Type = type;
			_Date = DateTime.Now;
		}

		public string Id { get => _Id; set => SetProperty(ref _Id, value); }
		string _Id;

		public string User { get => _User; set => SetProperty(ref _User, value); }
		string _User;

		public string BodyInstructions { get => _BodyInstructions; set => SetProperty(ref _BodyInstructions, value); }
		string _BodyInstructions;

		public string Body { get => _Body; set => SetProperty(ref _Body, value); }
		string _Body;

		[DefaultValue(false)]
		public bool IsPreview { get => _IsPreview; set => SetProperty(ref _IsPreview, value); }
		bool _IsPreview;

		[DefaultValue(false)]
		public bool IsAutomated { get => _IsAutomated; set => SetProperty(ref _IsAutomated, value); }
		bool _IsAutomated;

		[DefaultValue(false)]
		public bool IsTemp { get => _IsTemp; set => SetProperty(ref _IsTemp, value); }
		bool _IsTemp;

		[DefaultValue(0)]
		public int Tokens { get => _Tokens; set => SetProperty(ref _Tokens, value); }
		int _Tokens;

		/// <summary>Optional context data: cliboard, selection or Files.</summary>
		public BindingList<MessageAttachments> Attachments { get => _Attachments; set => SetProperty(ref _Attachments, value); }
		BindingList<MessageAttachments> _Attachments = new BindingList<MessageAttachments>();

		public DateTime Date { get => _Date; set => SetProperty(ref _Date, value); }
		DateTime _Date = DateTime.Now;

		public MessageType Type { get => _Type; set => SetProperty(ref _Type, value); }
		MessageType _Type;

		[XmlIgnore, JsonIgnore]
		public object Tag;

		/// <summary>
		/// Used for triggering updates on the HTML page. For example when attachment added.
		/// </summary>
		[XmlIgnore, JsonIgnore]
		public DateTime Updated { get => _Updated; set => SetProperty(ref _Updated, value); }
		DateTime _Updated;

		public MessageItem Copy(bool newId)
		{
			var copy = new MessageItem();
			copy.Id = Id;
			copy.User = User;
			copy.BodyInstructions = BodyInstructions;
			copy.Body = Body;
			copy.Date = Date;
			copy.Type = Type;
			foreach (var attachment in Attachments)
				copy.Attachments.Add(attachment);
			copy.IsEnabled = IsEnabled;
			if (newId)
				copy.Id = Guid.NewGuid().ToString("N");
			return copy;
		}

		public int UpdateTokens()
		{
			var tokens = 0;
			tokens += ClientHelper.CountTokens(Id, null);
			tokens += ClientHelper.CountTokens(User, null);
			tokens += ClientHelper.CountTokens(Body, null);
			tokens += ClientHelper.CountTokens(BodyInstructions, null);
			foreach (var attachment in Attachments)
				tokens += attachment.UpdateTokens();
			Tokens = tokens;
			return tokens;
		}

		#region Body Streaming Support

		/// <summary>
		/// The buffer to accumulate streamed text.
		/// </summary>
		[XmlIgnore, JsonIgnore]
		public string BodyBuffer { get => _BodyBuffer; }
		string _BodyBuffer;

		// An object to lock on for thread safety.
		private readonly object _BodyBufferLock = new object();

		/// <summary>
		/// Adds text to the BodyBuffer from the stream.
		/// </summary>
		public void AddToBodyBuffer(string text)
		{
			if (string.IsNullOrEmpty(text))
				return;
			lock (_BodyBufferLock)
			{
				_BodyBuffer += text;
				// Event handler must process the buffer if necessary.
				OnPropertyChanged(nameof(BodyBuffer));
				_Body += text;
				_BodyBuffer = string.Empty;
			}
		}

		#endregion


		#region ■ ISettingsItem

		public override bool IsEmpty =>
			string.IsNullOrEmpty(User) &&
			string.IsNullOrEmpty(Body);

		#endregion

	}
}
