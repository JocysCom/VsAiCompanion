namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class create_translation_response : chat_completion_message_tool_calls
    {
        public string text { get; set; }

    }
}
