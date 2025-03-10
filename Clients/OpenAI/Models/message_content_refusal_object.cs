using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class message_content_refusal_object : response_format_text
	{
		public string refusal { get; set; }

	}
}
