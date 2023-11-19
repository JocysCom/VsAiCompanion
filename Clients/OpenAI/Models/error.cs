namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class error : chat_completion_request_message_content_part_image
    {
        public string code { get; set; }

        public string message { get; set; }

        public string param { get; set; }

    }
}
