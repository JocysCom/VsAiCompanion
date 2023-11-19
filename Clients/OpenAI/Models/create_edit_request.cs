namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class create_edit_request : create_moderation_request
    {
        public string instruction { get; set; }

        public int? n { get; set; }

        public double? temperature { get; set; }

        public double? top_p { get; set; }

    }
}
