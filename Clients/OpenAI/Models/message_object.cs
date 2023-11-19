namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class message_object : thread_object
    {
        public string thread_id { get; set; }

        public string role { get; set; }

        public List<object> content { get; set; }

        public string assistant_id { get; set; }

        public string run_id { get; set; }

        public List<string> file_ids { get; set; }

    }
}
