namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class function_object : chat_completion_function_call_option
    {
        public string description { get; set; }

        public List<run_step_details_tool_calls_function_object> parameters { get; set; }

    }
}
