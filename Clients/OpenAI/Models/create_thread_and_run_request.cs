using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class create_thread_and_run_request : create_run_request
    {
        public create_thread_request thread { get; set; }

    }
}
