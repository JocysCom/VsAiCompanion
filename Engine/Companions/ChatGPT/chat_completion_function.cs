using System.ComponentModel;
using System.Text.Json.Serialization;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	/// <summary>
	/// Represents a function that can be called during chat completion.
	/// </summary>
	public class chat_completion_function : base_item
	{
		/// <summary>
		/// The unique identifier of the function.
		/// </summary>
		[Description("The unique identifier of the function.")]
		[JsonPropertyName("id")]
		public string id { get; set; }

		/// <summary>
		/// The name of the function.
		/// </summary>
		[Description("The name of the function.")]
		[JsonPropertyName("name")]
		public string name { get; set; }

		/// <summary>
		/// A description of what the function does.
		/// </summary>
		[Description("A description of what the function does.")]
		[JsonPropertyName("description")]
		public string description { get; set; }

		/// <summary>
		/// The parameters accepted by the function.
		/// </summary>
		[Description("The parameters accepted by the function.")]
		[JsonPropertyName("parameters")]
		public base_item parameters { get; set; }
	}
}
