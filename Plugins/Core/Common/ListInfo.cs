using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Plugins.Core.Common
{

	/// <summary>
	/// List Info.
	/// </summary>
	public class ListInfo
	{
		/// <summary>List name.</summary>
		public string Name { get; set; }
		/// <summary>List description.</summary>
		public string Description { get; set; }

		/// <summary>Dictionary items</summary>
		public Dictionary<string, string> Items { get; set; }

	}
}
