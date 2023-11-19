namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class error_response : chat_completion_message_tool_calls
    {
        public error error { get; set; }

    }
}
