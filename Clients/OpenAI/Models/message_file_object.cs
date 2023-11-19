namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class message_file_object : delete_assistant_file_response
    {
        public int created_at { get; set; }

        public string message_id { get; set; }

    }
}
