using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class @invite : user
	{
		public string status { get; set; }

		public int invited_at { get; set; }

		public int expires_at { get; set; }

		public int accepted_at { get; set; }

		public List<object> projects { get; set; }

	}
}
