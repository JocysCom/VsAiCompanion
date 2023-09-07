using System;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT
{
	/// <summary>
	/// Used for API services which supply invalid date.
	/// </summary>
	public class UnixTimestampConverter : JsonConverter<DateTime>
	{
		public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.String)
			{
				var s = reader.GetString();
				if (long.TryParse(s, out long unixSeconds))
					return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
				return DateTime.TryParse(s, out var date)
					? date
					: default;
			}
			if (reader.TokenType == JsonTokenType.Number)
				return DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64()).UtcDateTime;
			return default;
		}

		public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
		{
			writer.WriteStringValue(value.ToString());
		}
	}
}
