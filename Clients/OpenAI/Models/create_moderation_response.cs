namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class create_moderation_response : create_moderation_request
    {
        public string id { get; set; }

        public List<object> results { get; set; }

    }
}
