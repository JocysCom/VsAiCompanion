using System.ComponentModel;
using System.Text.Json.Serialization;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	/// <summary>
	/// The type of the tool. Currently, only 'function' is supported.
	/// </summary>
	[JsonConverter(typeof(JsonStringEnumConverter))]
	public enum chat_completion_tool_type
	{
		/// <summary>
		/// Function tool type.
		/// </summary>
		[Description("Function tool type.")]
		function,
	}
}
