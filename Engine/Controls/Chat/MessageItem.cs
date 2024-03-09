using JocysCom.ClassLibrary.Configuration;
using System;
using System.Collections.Generic;
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

		/// <summary>Optional context data: cliboard, selection or Files.</summary>
		public List<MessageAttachments> Attachments { get => _Attachments; set => SetProperty(ref _Attachments, value); }
		List<MessageAttachments> _Attachments = new List<MessageAttachments>();

		public DateTime Date { get => _Date; set => SetProperty(ref _Date, value); }
		DateTime _Date = DateTime.Now;

		public MessageType Type { get => _Type; set => SetProperty(ref _Type, value); }
		MessageType _Type;

		[XmlIgnore, JsonIgnore]
		public object Tag;

		public MessageItem Copy(bool newId)
		{
			var copy = new MessageItem();
			copy.Id = Id;
			copy.User = User;
			copy.BodyInstructions = BodyInstructions;
			copy.Body = Body;
			copy.Date = Date;
			copy.Type = Type;
			copy.Attachments.AddRange(Attachments);
			copy.IsEnabled = IsEnabled;
			if (newId)
				copy.Id = Guid.NewGuid().ToString("N");
			return copy;
		}

		#region ■ ISettingsItem

		public override bool IsEmpty =>
			string.IsNullOrEmpty(User) &&
			string.IsNullOrEmpty(Body);

		#endregion

	}
}
