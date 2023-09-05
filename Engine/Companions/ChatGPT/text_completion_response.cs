using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public class text_completion_response: base_completion_response
	{
		public List<text_choice> choices { get; set; }
	}
}
