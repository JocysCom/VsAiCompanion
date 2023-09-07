using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public partial class chat_completion_request : base_completion_request

	{
		public List<chat_completion_message> messages { get; set; } = new List<chat_completion_message>();
		public List<chat_completion_function> functions { get; set; }
		public function_call function_call { get; set; }
	}
}
