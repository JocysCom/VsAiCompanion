using JocysCom.ClassLibrary.Configuration;
using System.Collections.Generic;
using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine.Audio
{
	public class VoiceProperties : SettingsItem
	{
		[DefaultValue(false)]
		public bool IsFavorite { get => _IsFavorite; set => SetProperty(ref _IsFavorite, value); }
		bool _IsFavorite = true;

		#region Read Only Fields

		public string Name { get; set; }
		public string DisplayName { get; set; }
		public string LocalName { get; set; }
		public string ShortName { get; set; }
		public string Gender { get; set; }
		public string Locale { get; set; }
		public string LocaleName { get; set; }
		public List<string> StyleList { get; set; }
		public string SampleRateHertz { get; set; }
		public string VoiceType { get; set; }
		public string Status { get; set; }
		public List<string> SecondaryLocaleList { get; set; }
		public List<string> RolePlayList { get; set; }
		public string WordsPerMinute { get; set; }
		public Dictionary<string, string> ExtendedPropertyMap { get; set; }

		#endregion

	}

}
