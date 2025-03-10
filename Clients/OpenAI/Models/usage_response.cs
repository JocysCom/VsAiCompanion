using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class usage_response : list_paginated_fine_tuning_jobs_response
	{
		public string next_page { get; set; }

	}
}
