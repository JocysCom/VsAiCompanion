using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class create_translation_request : create_moderation_request
    {
        public string file { get; set; }

        public string prompt { get; set; }

        public string response_format { get; set; }

        public double temperature { get; set; }

    }
}
