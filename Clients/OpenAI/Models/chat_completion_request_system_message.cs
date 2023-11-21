using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class chat_completion_request_system_message : error_response
    {
        public string content { get; set; }

        public string role { get; set; }

    }
}
