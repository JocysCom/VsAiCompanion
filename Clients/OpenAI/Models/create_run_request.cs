namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class create_run_request : create_moderation_request
    {
        public string assistant_id { get; set; }

        public string instructions { get; set; }

        public List<object> tools { get; set; }

        public object metadata { get; set; }

    }
}
