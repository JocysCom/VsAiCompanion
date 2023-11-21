using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class image : error_response
    {
        public string b64_json { get; set; }

        public string url { get; set; }

        public string revised_prompt { get; set; }

    }
}
