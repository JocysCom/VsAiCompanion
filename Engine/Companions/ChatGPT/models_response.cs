using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{

	public partial class models_response: base_response
	{
		public string @object { get; set; }
		public List<model> data { get; set; }

	}

}
