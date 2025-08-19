using System;

namespace JocysCom.VS.AiCompanion.Engine.Mcp.Protocol
{
	/// <summary>
	/// Exception thrown when MCP operations fail
	/// </summary>
	public class McpException : Exception
	{
		public McpException() : base()
		{
		}

		public McpException(string message) : base(message)
		{
		}

		public McpException(string message, Exception innerException) : base(message, innerException)
		{
		}

		/// <summary>
		/// JSON-RPC error code if available
		/// </summary>
		public int? ErrorCode { get; set; }

		/// <summary>
		/// Additional error data from JSON-RPC error
		/// </summary>
		public object ErrorData { get; set; }
	}
}
