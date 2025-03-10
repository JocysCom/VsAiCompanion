using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class response_format_json_schema : response_format_text
	{
		public object json_schema { get; set; }

	}
}
