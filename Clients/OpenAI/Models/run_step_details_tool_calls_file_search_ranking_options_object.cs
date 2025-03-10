using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class run_step_details_tool_calls_file_search_ranking_options_object : add_upload_part_request
	{
		public string ranker { get; set; }

		public double score_threshold { get; set; }

	}
}
