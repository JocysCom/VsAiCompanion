namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class chat_completion_request_user_message : error_response
    {
        public object content { get; set; }

        public string role { get; set; }

    }
}
