using System;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public partial class model: base_response
	{
		public string id { get; set; }

		public string @object { get; set; }

		public DateTime created { get; set; }

		public string owned_by { get; set; }

	}


}
