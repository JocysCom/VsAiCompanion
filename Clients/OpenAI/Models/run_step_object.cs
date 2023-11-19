namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class run_step_object : message_object
    {
        public string type { get; set; }

        public string status { get; set; }

        public object step_details { get; set; }

        public object last_error { get; set; }

        public int? expired_at { get; set; }

        public int? cancelled_at { get; set; }

        public int? failed_at { get; set; }

        public int? completed_at { get; set; }

    }
}
