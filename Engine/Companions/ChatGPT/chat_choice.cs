namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public class chat_choice : base_item
	{
		public int index { get; set; }

		public chat_completion_message delta { get; set; }

		public chat_completion_message message { get; set; }

		public choice_finish_reason? finish_reason { get; set; }


	}
}
