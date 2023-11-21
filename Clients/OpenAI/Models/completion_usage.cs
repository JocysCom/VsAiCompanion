using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class completion_usage : error_response
    {
        public int completion_tokens { get; set; }

        public int prompt_tokens { get; set; }

        public int total_tokens { get; set; }

    }
}
