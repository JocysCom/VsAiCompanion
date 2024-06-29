using System.Collections.Generic;
using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public class tool_item : base_item
	{
		[DefaultValue(null)]
		public string name { get; set; }
		[DefaultValue(null)]
		public string type { get; set; }
		[DefaultValue(null)]
		public string description { get; set; }
		[DefaultValue(null)]
		public string[] @enum { get; set; }
		[DefaultValue(null)]
		public tool_item items { get; set; }
		[DefaultValue(null)]
		public Dictionary<string, tool_item> properties { get; set; }
		public tool_item parameters { get; set; }
		public string[] required { get; set; }

		// Used by dateTime type.
		public string format;

	}
}
