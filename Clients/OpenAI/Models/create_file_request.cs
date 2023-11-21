using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class create_file_request : error_response
    {
        public string file { get; set; }

        public string purpose { get; set; }

    }
}
