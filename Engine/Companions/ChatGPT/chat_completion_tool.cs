namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	public class chat_completion_tool : base_item
	{
		/// <summary>The type of the tool. Currently, only `function` is supported.</summary>
		public chat_completion_tool_type type { get; set; }

		/// <summary>The function that the model called.</summary>
		public chat_completion_function function { get; set; }

	}
}
