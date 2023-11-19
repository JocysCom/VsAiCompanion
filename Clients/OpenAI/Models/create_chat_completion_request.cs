namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class create_chat_completion_request : create_completion_request
    {
        public List<chat_completion_message_tool_calls> messages { get; set; }

        public object response_format { get; set; }

        public List<assistant_tools_function> tools { get; set; }

        public List<run_step_details_tool_calls_function_object> tool_choice { get; set; }

        public object function_call { get; set; }

        public List<function_object> functions { get; set; }

    }
}
