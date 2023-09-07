namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public class chat_completion_message: base_item
	{
		public message_role role { get; set; }

		public string content { get; set; }

		public string name { get; set; }

		public function_call function_call { get; set; }

	}
}
