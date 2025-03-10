using System.Collections.Generic;
namespace JocysCom.VS.AiCompanion.Clients.OpenAI.Models
{
	public class create_image_edit_request : create_image_variation_request
	{
		public string prompt { get; set; }

		public string mask { get; set; }

	}
}
