using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class images_response : error_response
    {
        public int created { get; set; }

        public List<image> data { get; set; }

    }
}
