namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class assistant_tools_retrieval : chat_completion_message_tool_calls
    {
        public string type { get; set; }

    }
}
