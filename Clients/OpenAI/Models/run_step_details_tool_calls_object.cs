using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class run_step_details_tool_calls_object : assistant_tools_retrieval
    {
        public List<object> tool_calls { get; set; }

    }
}
