using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class chat_completion_request_tool_message : chat_completion_request_system_message
    {
        public string tool_call_id { get; set; }

    }
}
