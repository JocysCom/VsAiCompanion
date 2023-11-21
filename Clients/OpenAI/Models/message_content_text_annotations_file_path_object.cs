using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class message_content_text_annotations_file_path_object : chat_completion_request_message_content_part_text
    {
        public object file_path { get; set; }

        public int start_index { get; set; }

        public int end_index { get; set; }

    }
}
