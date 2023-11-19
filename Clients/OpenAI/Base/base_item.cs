using System.Text.Json;
using System.Text.Json.Serialization;

namespace JocysCom.VS.AiCompanion.Clients.OpenAI
{
	public class base_item
	{
		/// <summary>
		/// This property is used to capture additional JSON properties that aren't explicitly defined in the class.
		/// These are stored as KeyValue pairs, where the 'Key' is the property name and 'Value' is the property value.
		/// </summary>
		[JsonExtensionData]
		public Dictionary<string, JsonElement> additional_properties { get; set; }
	}
}
