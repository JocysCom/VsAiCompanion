using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	/// <summary>
	/// Represents a tool call message in chat completion.
	/// </summary>
	public class chat_completion_message_tool_call : chat_completion_tool
	{
		/// <summary>
		/// The ID of the tool call. Must have format 'call_[randomid]'.
		/// </summary>
		[Required]
		[RegularExpression(@"^call_[a-zA-Z0-9]{34}$", ErrorMessage = "ID must start with 'call_'")]
		[Description("The ID of the tool call. Must have format 'call_<randomid>'. For example: 'call_W6C7NV3fSVomN4zgw1EBNlNI'")]
		[JsonPropertyName("id")]
		public string id { get; set; }
	}
}
