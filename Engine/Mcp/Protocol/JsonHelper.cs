using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JocysCom.VS.AiCompanion.Engine.Mcp.Protocol
{
	/// <summary>
	/// JSON serialization helper for MCP protocol
	/// </summary>
	public static class JsonHelper
	{
		private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			WriteIndented = false,
			Converters = { new JsonStringEnumConverter() }
		};

		/// <summary>
		/// Serialize object to JSON string
		/// </summary>
		public static string Serialize(object obj)
		{
			if (obj == null)
				return "null";

			return JsonSerializer.Serialize(obj, _options);
		}

		/// <summary>
		/// Deserialize JSON string to typed object
		/// </summary>
		public static T Deserialize<T>(object jsonObj)
		{
			if (jsonObj == null)
				return default(T);

			string json;
			if (jsonObj is string str)
			{
				json = str;
			}
			else if (jsonObj is JsonElement element)
			{
				json = element.GetRawText();
			}
			else
			{
				json = JsonSerializer.Serialize(jsonObj, _options);
			}

			return JsonSerializer.Deserialize<T>(json, _options);
		}

		/// <summary>
		/// Parse JSON-RPC message from string
		/// </summary>
		public static JsonRpcMessage ParseMessage(string json)
		{
			if (string.IsNullOrEmpty(json))
				return null;

			var document = JsonDocument.Parse(json);
			var root = document.RootElement;

			// Check if it's a response (has 'result' or 'error' property)
			if (root.TryGetProperty("result", out _) || root.TryGetProperty("error", out _))
			{
				return JsonSerializer.Deserialize<JsonRpcResponse>(json, _options);
			}
			// Check if it's a request (has 'id' property)
			else if (root.TryGetProperty("id", out _))
			{
				return JsonSerializer.Deserialize<JsonRpcRequest>(json, _options);
			}
			// Otherwise it's a notification
			else
			{
				return JsonSerializer.Deserialize<JsonRpcNotification>(json, _options);
			}
		}

		/// <summary>
		/// Convert object to JsonElement for dynamic handling
		/// </summary>
		public static JsonElement ToJsonElement(object obj)
		{
			if (obj == null)
				return new JsonElement();

			var json = Serialize(obj);
			var document = JsonDocument.Parse(json);
			return document.RootElement;
		}
	}
}
