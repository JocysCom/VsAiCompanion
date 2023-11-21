using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class assistant_tools_function : assistant_tools_retrieval
    {
        public function_object function { get; set; }

    }
}
