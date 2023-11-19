namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class submit_tool_outputs_run_request : chat_completion_message_tool_calls
    {
        public List<object> tool_outputs { get; set; }

    }
}
