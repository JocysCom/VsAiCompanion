using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class chat_completion_stream_response_delta : chat_completion_request_system_message
    {
        public object function_call { get; set; }

        public List<chat_completion_message_tool_call_chunk> tool_calls { get; set; }

    }
}
