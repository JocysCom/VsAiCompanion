using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	/// <summary>
	/// Base class that captures additional JSON properties not explicitly defined in derived classes.
	/// </summary>
	public class base_item
	{
		/// <summary>
		/// This property is used to capture additional JSON properties that aren't explicitly defined in the class.
		/// These are stored as KeyValue pairs, where the 'Key' is the property name and 'Value' is the property value.
		/// </summary>
		[JsonExtensionData]
		public Dictionary<string, JsonElement> additional_properties { get; set; } = new Dictionary<string, JsonElement>();

		/// <summary>
		/// Add custom additional property.
		/// </summary>
		/// <param name="key">Property name.</param>
		/// <param name="value">Property value.</param>
		public void AddProperty(string key, object value)
		{
			var jsonString = JsonSerializer.Serialize(value);
			using (JsonDocument document = JsonDocument.Parse(jsonString))
			{
				JsonElement element = document.RootElement.Clone();
				additional_properties[key] = element;
			}
		}

		/// <summary>
		/// Get value of additional property.
		/// </summary>
		/// <typeparam name="T">Value type.</typeparam>
		/// <param name="key">Property name.</param>
		/// <returns>Property value.</returns>
		public T GetProperty<T>(string key)
		{
			return additional_properties.TryGetValue(key, out JsonElement element)
				? JsonSerializer.Deserialize<T>(element.GetRawText())
				: default;
		}
	}
}
