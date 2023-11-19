namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class run_object : run_step_object
    {
        public object required_action { get; set; }

        public int expires_at { get; set; }

        public int? started_at { get; set; }

        public string model { get; set; }

        public string instructions { get; set; }

        public List<object> tools { get; set; }

    }
}
