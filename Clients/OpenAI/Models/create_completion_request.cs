using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
    public class create_completion_request : create_edit_request
    {
        public object prompt { get; set; }

        public int? best_of { get; set; }

        public bool echo { get; set; }

        public double? frequency_penalty { get; set; }

        public object logit_bias { get; set; }

        public int? logprobs { get; set; }

        public int? max_tokens { get; set; }

        public double? presence_penalty { get; set; }

        public int? seed { get; set; }

        public object stop { get; set; }

        public bool stream { get; set; }

        public string suffix { get; set; }

        public string user { get; set; }

    }
}
