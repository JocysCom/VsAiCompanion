namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public class chat_completion_message_tool_call : chat_completion_tool
	{
		/// <summary>The ID of the tool call.</summary>
		public string id { get; set; }
	}
}
