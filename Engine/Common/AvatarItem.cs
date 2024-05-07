using JocysCom.ClassLibrary.ComponentModel;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class AvatarItem : NotifyPropertyChanged
	{
		public AvatarItem()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
		}

		public Guid AiServiceId { get => _AiServiceId; set => SetProperty(ref _AiServiceId, value); }
		Guid _AiServiceId;

		public string Message { get => _Message; set => SetProperty(ref _Message, value); }
		string _Message;

		[DefaultValue(true)]
		public bool CacheAudioData { get => _CacheAudioData; set => SetProperty(ref _CacheAudioData, value); }
		bool _CacheAudioData;

		[DefaultValue(null)]
		public string VoiceName { get => _VoiceName; set => SetProperty(ref _VoiceName, value); }
		string _VoiceName;

		[DefaultValue(null)]
		public BindingList<string> VoiceNames
		{
			get => _VoiceNames = _VoiceNames ?? new BindingList<string>() { VoiceName };
			set => SetProperty(ref _VoiceNames, value);
		}
		BindingList<string> _VoiceNames;

		[XmlIgnore, JsonIgnore]
		public string AiServiceName { get => Global.AppSettings?.AiServices?.FirstOrDefault(x => x.Id == AiServiceId)?.Name; }

	}
}
