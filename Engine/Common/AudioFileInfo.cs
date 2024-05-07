using JocysCom.ClassLibrary.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class AudioFileInfo : NotifyPropertyChanged
	{
		[DefaultValue(false)]
		public bool IsSsml { get => _IsSsml; set => SetProperty(ref _IsSsml, value); }
		bool _IsSsml;

		[DefaultValue("")]
		public string Text { get => _Text = _Text ?? ""; set => SetProperty(ref _Text, value); }
		string _Text;

		public TimeSpan AudioDuration { get => _AudioDuration; set => SetProperty(ref _AudioDuration, value); }
		TimeSpan _AudioDuration;

		[DefaultValue(null)]
		public List<VisemeItem> Viseme { get => _Viseme = _Viseme ?? new List<VisemeItem>(); set => SetProperty(ref _Viseme, value); }
		List<VisemeItem> _Viseme;

	}
}
