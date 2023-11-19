namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class create_assistant_file_request : chat_completion_message_tool_calls
    {
        public string file_id { get; set; }

    }
}
