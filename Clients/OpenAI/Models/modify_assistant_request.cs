namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class modify_assistant_request : create_run_request
    {
        public string name { get; set; }

        public string description { get; set; }

        public List<string> file_ids { get; set; }

    }
}
