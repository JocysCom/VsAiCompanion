namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class message_content_text_annotations_file_citation_object : message_content_text_object
	{
		public object file_citation { get; set; }

		public int start_index { get; set; }

		public int end_index { get; set; }

	}
}
