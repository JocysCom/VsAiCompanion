using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using System.ComponentModel;
using System.Text.Json;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Chat
{
	public class MessageAttachments : NotifyPropertyChanged
	{
		public MessageAttachments()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
		}

		public MessageAttachments(ContextType attachmentType, string language, string data)
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
			Title = ClassLibrary.Runtime.Attributes.GetDescription(attachmentType);
			Type = attachmentType;
			Data = $"```{language}\r\n{data}\r\n```";
			IsMarkdown = true;
		}

		public MessageAttachments(ContextType attachmentType, object dataToJson)
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
			Title = ClassLibrary.Runtime.Attributes.GetDescription(attachmentType);
			Type = attachmentType;
			var options = new JsonSerializerOptions();
			options.WriteIndented = true;
			var data = JsonSerializer.Serialize(dataToJson, options);
			Data = $"```json\r\n{data}\r\n```";
			IsMarkdown = true;
		}


		/// <summary>
		/// If true, the attachment will always be included as part of the body.
		/// </summary>
		[DefaultValue(false)]
		public bool IsAlwaysIncluded { get => _IsAlwaysIncluded; set => SetProperty(ref _IsAlwaysIncluded, value); }
		bool _IsAlwaysIncluded;

		public bool IsMarkdown { get => _IsMarkdown; set => SetProperty(ref _IsMarkdown, value); }
		bool _IsMarkdown;

		public ContextType Type { get => _Type; set => SetProperty(ref _Type, value); }
		ContextType _Type;

		public string Title { get => _Title; set => SetProperty(ref _Title, value); }
		string _Title;

		/// <summary>
		/// File path or web link.
		/// </summary>
		[DefaultValue(null)]
		public string Location { get => _Location; set => SetProperty(ref _Location, value); }
		string _Location;

		public string Instructions { get => _Instructions; set => SetProperty(ref _Instructions, value); }
		string _Instructions;

		/// <summary>Optional context data: cliboard, selection or Files.</summary>
		public string Data { get => _Data; set => SetProperty(ref _Data, value); }
		string _Data;

		[DefaultValue(message_role.user)]
		public message_role MessageRole { get => _MessageRole; set => SetProperty(ref _MessageRole, value); }
		message_role _MessageRole;

	}
}
