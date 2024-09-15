using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	/// <summary>
	/// Represents a tool used in chat completion.
	/// </summary>
	public class chat_completion_tool : base_item
	{
		/// <summary>
		/// The type of the tool. Currently, only 'function' is supported.
		/// </summary>
		[Required]
		[Description("The type of the tool. Currently, only 'function' is supported.")]
		[JsonPropertyName("type")]
		public chat_completion_tool_type type { get; set; }

		/// <summary>
		/// The function that the model called.
		/// </summary>
		[Description("The function that the model called.")]
		[JsonPropertyName("function")]
		public chat_completion_function function { get; set; }
	}
}
