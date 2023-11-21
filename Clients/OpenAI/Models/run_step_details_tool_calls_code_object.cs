using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class run_step_details_tool_calls_code_object : chat_completion_request_message_content_part_image
    {
        public string id { get; set; }

        public object code_interpreter { get; set; }

    }
}
