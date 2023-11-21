using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class function_object : chat_completion_function_call_option
    {
        public string description { get; set; }

        public chat_completion_message_tool_calls parameters { get; set; }

    }
}
