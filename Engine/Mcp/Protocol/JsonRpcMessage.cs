using System.Text.Json.Serialization;

namespace JocysCom.VS.AiCompanion.Engine.Mcp.Protocol
{
	/// <summary>
	/// Base class for all JSON-RPC 2.0 messages
	/// </summary>
	public abstract class JsonRpcMessage
	{
		[JsonPropertyName("jsonrpc")]
		public string JsonRpc { get; set; } = "2.0";
	}

	/// <summary>
	/// JSON-RPC 2.0 Request message
	/// </summary>
	public class JsonRpcRequest : JsonRpcMessage
	{
		[JsonPropertyName("id")]
		public string Id { get; set; }

		[JsonPropertyName("method")]
		public string Method { get; set; }

		[JsonPropertyName("params")]
		public object Params { get; set; }
	}

	/// <summary>
	/// JSON-RPC 2.0 Response message
	/// </summary>
	public class JsonRpcResponse : JsonRpcMessage
	{
		[JsonPropertyName("id")]
		public string Id { get; set; }

		[JsonPropertyName("result")]
		public object Result { get; set; }

		[JsonPropertyName("error")]
		public JsonRpcError Error { get; set; }
	}

	/// <summary>
	/// JSON-RPC 2.0 Notification message
	/// </summary>
	public class JsonRpcNotification : JsonRpcMessage
	{
		[JsonPropertyName("method")]
		public string Method { get; set; }

		[JsonPropertyName("params")]
		public object Params { get; set; }
	}

	/// <summary>
	/// JSON-RPC 2.0 Error object
	/// </summary>
	public class JsonRpcError
	{
		[JsonPropertyName("code")]
		public int Code { get; set; }

		[JsonPropertyName("message")]
		public string Message { get; set; }

		[JsonPropertyName("data")]
		public object Data { get; set; }
	}
}
