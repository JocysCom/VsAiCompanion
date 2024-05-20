using JocysCom.ClassLibrary.ComponentModel;
using JocysCom.VS.AiCompanion.Engine.Speech;
using JocysCom.VS.AiCompanion.Plugins.Core.TtsMonitor;
using System;
using System.ComponentModel;
using System.Linq;
using System.Speech.AudioFormat;
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

		#region Audio File Cache

		[DefaultValue(false)]
		public bool CacheAudioConvert { get { return _CacheAudioConvert; } set { _CacheAudioConvert = value; OnPropertyChanged(); } }
		bool _CacheAudioConvert;

		[DefaultValue(AudioFileFormat.MP3)]
		public AudioFileFormat CacheAudioFormat { get { return _CacheAudioFormat; } set { _CacheAudioFormat = value; OnPropertyChanged(); } }
		AudioFileFormat _CacheAudioFormat = AudioFileFormat.ULaw;

		[DefaultValue(AudioChannel.Mono)]
		public AudioChannel CacheAudioChannels { get { return _CacheAudioChannels; } set { _CacheAudioChannels = value; OnPropertyChanged(); } }
		AudioChannel _CacheAudioChannels = AudioChannel.Mono;

		[DefaultValue(22050)]
		public int CacheAudioSampleRate { get { return _CacheAudioSampleRate; } set { _CacheAudioSampleRate = value; OnPropertyChanged(); } }
		int _CacheAudioSampleRate = 22050;

		[DefaultValue(16)]
		public int CacheAudioBitsPerSample { get { return _CacheAudioBitsPerSample; } set { _CacheAudioBitsPerSample = value; OnPropertyChanged(); } }
		int _CacheAudioBitsPerSample = 16;

		// Data rates(hence, bit-rates) are always expressed in powers of 10, not 2.
		// 128 kilobits per second = 128,000 bits per second = 16,000 bytes per second.
		//
		// 32 kbit/s – generally acceptable only for speech
		// 96 kbit/s – generally used for speech or low-quality streaming
		// 128 or 160 kbit/s – mid-range bitrate quality
		// 192 kbit/s – medium quality bitrate
		// 256 kbit/s – a commonly used high-quality bitrate
		// 320 kbit/s – highest level supported by the MP3 standard	
		//	
		[DefaultValue(128000)]
		public int CacheAudioAverageBitsPerSecond { get { return _CacheAudioAverageBitsPerSecond; } set { _CacheAudioAverageBitsPerSecond = value; OnPropertyChanged(); } }
		int _CacheAudioAverageBitsPerSecond = 128000;

		// Block Alignment = Bytes per Sample x Number of Channels
		// For example, the block alignment value for 16-bit PCM format mono audio is 2 (2 bytes per sample x 1 channel). For 16-bit PCM format stereo audio, the block alignment value is 4.
		[DefaultValue(2)]
		public int CacheAudioBlockAlign { get { return _CacheAudioBlockAlign; } set { _CacheAudioBlockAlign = value; OnPropertyChanged(); } }
		int _CacheAudioBlockAlign = 2;

		#endregion

	}
}
