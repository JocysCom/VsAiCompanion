using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class create_thread_request : modify_message_request
    {
        public List<create_message_request> messages { get; set; }

    }
}
