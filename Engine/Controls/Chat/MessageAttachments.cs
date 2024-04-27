using JocysCom.VS.AiCompanion.Plugins.Core.VsFunctions;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Chat
{
	public class MessageAttachments : INotifyPropertyChanged
	{
		public MessageAttachments() { }

		public MessageAttachments(ContextType attachmentType, string language, string data)
		{
			Title = ClassLibrary.Runtime.Attributes.GetDescription(attachmentType);
			Type = attachmentType;
			Data = $"```{language}\r\n{data}\r\n```";
			IsMarkdown = true;
		}

		public MessageAttachments(ContextType attachmentType, object dataToJson)
		{
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
