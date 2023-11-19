namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class create_message_request : chat_completion_request_system_message
    {
        public List<string> file_ids { get; set; }

        public object metadata { get; set; }

    }
}
