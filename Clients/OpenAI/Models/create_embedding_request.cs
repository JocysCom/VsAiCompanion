namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class create_embedding_request : create_moderation_request
    {
        public string encoding_format { get; set; }

        public string user { get; set; }

    }
}
