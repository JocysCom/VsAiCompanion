using System.Collections.Generic;
using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	/*
	 {"model":"gpt-3.5-turbo-16k","messages":[{"role":"user","content":"say hi\r\n\r\n","name":"User"}],"temperature":1,"stream":false,"presence_penalty":0,"frequency_penalty":0}
	 */

	public partial class chat_completion_request : base_item
	{
		public string model { get; set; }

		public List<chat_completion_message> messages { get; set; } = new List<chat_completion_message>();

		public List<chat_completion_function> functions { get; set; }

		public function_call function_call { get; set; }

		public double? temperature { get; set; }

		public double? top_p { get; set; }

		public int? n { get; set; }

		public bool? stream { get; set; }

		public List<string> stop { get; set; }

		public int max_tokens { get; set; }

		public double? presence_penalty { get; set; }

		public double? frequency_penalty { get; set; }

		public object logit_bias { get; set; }

		public string user { get; set; }

	}
}
