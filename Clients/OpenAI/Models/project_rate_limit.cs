namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class project_rate_limit : project_rate_limit_update_request
	{
		public string @object { get; set; }

		public string id { get; set; }

		public string model { get; set; }

	}
}
