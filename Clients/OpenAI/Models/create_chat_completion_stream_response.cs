namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class create_chat_completion_stream_response : create_edit_response
    {
        public string id { get; set; }

        public string model { get; set; }

        public string system_fingerprint { get; set; }

    }
}
