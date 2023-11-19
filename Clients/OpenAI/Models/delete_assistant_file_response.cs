namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class delete_assistant_file_response : list_fine_tune_events_response
    {
        public string id { get; set; }

        public bool deleted { get; set; }

    }
}
