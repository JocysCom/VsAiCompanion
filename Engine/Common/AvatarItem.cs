using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.VS.AiCompanion.Plugins.Core.TtsMonitor;
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

		[DefaultValue(false)]
		public bool AlwaysOnTop { get => _AlwaysOnTop; set => SetProperty(ref _AlwaysOnTop, value); }
		private bool _AlwaysOnTop;

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
		public string VoiceLocale { get => _VoiceLocale; set => SetProperty(ref _VoiceLocale, value); }
		string _VoiceLocale;

		[DefaultValue(VoiceGender.Male)]
		public VoiceGender Gender { get => _Gender; set => SetProperty(ref _Gender, value); }
		VoiceGender _Gender;

		/// <summary>
		/// Instructions to send when avatar is visible.
		/// </summary>
		[DefaultValue(null)]
		public string Instructions { get => _Instructions; set => SetProperty(ref _Instructions, value); }
		string _Instructions;


		[XmlIgnore, JsonIgnore]
		public string AiServiceName { get => Global.AppSettings?.AiServices?.FirstOrDefault(x => x.Id == AiServiceId)?.Name; }

	}
}
