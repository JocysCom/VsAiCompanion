using System.Text.Json;
using System.Text.Json.Serialization;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	/// <summary>
	/// JSON serialization utilities for the ChatGPT client.
	/// </summary>
	public partial class Client
	{
		#region JSON Serialization

		/// <summary>
		/// Creates and configures JsonSerializerOptions for API communication.
		/// </summary>
		/// <param name="writeIndented">Whether to format JSON with indentation for readability.</param>
		/// <returns>Configured JsonSerializerOptions instance.</returns>
		public static JsonSerializerOptions GetJsonOptions(bool writeIndented = false)
		{
			var o = new JsonSerializerOptions();
			o.WriteIndented = writeIndented;
			o.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
			o.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
			o.Converters.Add(new UnixTimestampConverter());
			o.Converters.Add(new JsonStringEnumConverter());
			return o;
		}

		/// <summary>
		/// Gets the default JsonSerializerOptions for API operations.
		/// </summary>
		static JsonSerializerOptions JsonOptions
		{
			get
			{
				if (_JsonOptions == null)
					_JsonOptions = GetJsonOptions();
				return _JsonOptions;
			}
		}
		static JsonSerializerOptions _JsonOptions;

		/// <summary>
		/// Deserializes JSON string to the specified type using configured options.
		/// </summary>
		/// <typeparam name="T">The type to deserialize to.</typeparam>
		/// <param name="json">The JSON string to deserialize.</param>
		/// <returns>Deserialized object of type T.</returns>
		public static T Deserialize<T>(string json)
			=> JsonSerializer.Deserialize<T>(json, JsonOptions);

		/// <summary>
		/// Serializes an object to JSON string using configured options.
		/// </summary>
		/// <param name="o">The object to serialize.</param>
		/// <param name="writeIndented">Whether to format JSON with indentation.</param>
		/// <returns>JSON string representation of the object.</returns>
		public static string Serialize(object o, bool writeIndented = false)
			=> writeIndented
			? JsonSerializer.Serialize(o, GetJsonOptions(writeIndented))
			: JsonSerializer.Serialize(o, JsonOptions);

		#endregion
	}
}
