namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class create_image_variation_request : create_embedding_request
    {
        public string image { get; set; }

        public int? n { get; set; }

        public string? response_format { get; set; }

        public string? size { get; set; }

    }
}
