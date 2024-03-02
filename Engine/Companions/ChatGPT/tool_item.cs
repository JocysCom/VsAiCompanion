using CsvHelper.Configuration.Attributes;
using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public class tool_item : base_item
	{
		[Default(null)]
		public string name { get; set; }
		[Default(null)]
		public string type { get; set; }
		[Default(null)]
		public string description { get; set; }
		[Default(null)]
		public string[] @enum { get; set; }
		[Default(null)]
		public tool_item items { get; set; }
		[Default(null)]
		public Dictionary<string, tool_item> properties { get; set; }
		public tool_item parameters { get; set; }
		public string[] required { get; set; }

		// Used by dateTime type.
		public string format;

	}
}
