namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class list_fine_tune_events_response : error_response
    {
        public List<fine_tune_event> data { get; set; }

        public string @object { get; set; }

    }
}
