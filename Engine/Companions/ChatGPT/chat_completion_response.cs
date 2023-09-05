using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	internal class chat_completion_response: base_completion_response
	{
		public List<chat_choice> choices { get; set; }
	}
}
