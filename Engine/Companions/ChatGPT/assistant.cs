using System.Collections.Generic;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	/// <summary>
	/// Represents an assistant that can call the model and use tools.
	/// </summary>
	public partial class assistant : base_object
	{
		/// <summary>
		/// Name of the assistant.
		/// </summary>
		public string name { get; set; }

		/// <summary>
		/// Description of the assistant.
		/// </summary>
		public string description { get; set; }

		/// <summary>
		/// ID of the model to use.
		/// </summary>
		public string model { get; set; }

		/// <summary>
		/// System instructions for the assistant.
		/// </summary>
		public string instructions { get; set; }

		/// <summary>
		/// List of tools enabled on the assistant.
		/// </summary>
		public List<string> tools { get; set; }

		/// <summary>
		/// List of file IDs attached to this assistant.
		/// </summary>
		public List<string> file_ids { get; set; }

		/// <summary>
		/// Metadata in key-value pairs.
		/// </summary>
		public Dictionary<string, string> metadata { get; set; }
	}
}
