using JocysCom.ClassLibrary.Configuration;
using JocysCom.VS.AiCompanion.Engine;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace JocysCom.ClassLibrary.Controls.Chat
{
	public class MessageItem : ISettingsItem, INotifyPropertyChanged
	{
		public MessageItem()
		{
			_Id = Guid.NewGuid().ToString("N");
			Date = DateTime.Now;
		}

		public string Id { get => _Id; set => SetProperty(ref _Id, value); }
		string _Id;

		public string User { get => _User; set => SetProperty(ref _User, value); }
		string _User;

		public string BodyInstructions { get => _BodyInstructions; set => SetProperty(ref _BodyInstructions, value); }
		string _BodyInstructions;

		public string Body { get => _Body; set => SetProperty(ref _Body, value); }
		string _Body;

		/// <summary>Optional context data: cliboard, selection or Files.</summary>
		public List<MessageAttachments> Attachments { get => _Attachments; set => SetProperty(ref _Attachments, value); }
		List<MessageAttachments> _Attachments = new List<MessageAttachments>();

		public DateTime Date { get => _Date; set => SetProperty(ref _Date, value); }
		DateTime _Date = DateTime.Now;

		public MessageType Type { get => _Type; set => SetProperty(ref _Type, value); }
		MessageType _Type;

		public bool IsEnabled { get => _IsEnabled; set => SetProperty(ref _IsEnabled, value); }
		bool _IsEnabled;

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
		bool ISettingsItem.Enabled { get => IsEnabled; set => IsEnabled = value; }

		public bool IsEmpty =>
			string.IsNullOrEmpty(User) &&
			string.IsNullOrEmpty(Body);

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
