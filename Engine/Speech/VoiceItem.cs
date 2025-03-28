﻿using JocysCom.ClassLibrary.Configuration;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine.Speech
{
	public class VoiceItem : SettingsFileItem
	{

		public VoiceItem()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
		}

		[DefaultValue(false)]
		public bool IsFavorite { get => _IsFavorite; set => SetProperty(ref _IsFavorite, value); }
		bool _IsFavorite;

		#region Read Only Fields (Azure)

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

		[XmlIgnore, JsonIgnore]
		public Dictionary<string, string> ExtendedPropertyMap { get; set; }

		#endregion

	}

}
