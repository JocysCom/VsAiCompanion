namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class create_moderation_request : error_response
    {
        public object input { get; set; }

        public object model { get; set; }

    }
}
