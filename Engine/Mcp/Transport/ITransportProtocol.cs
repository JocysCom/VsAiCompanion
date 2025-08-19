using System;
using System.Threading;
using System.Threading.Tasks;
using JocysCom.VS.AiCompanion.Engine.Mcp.Protocol;

namespace JocysCom.VS.AiCompanion.Engine.Mcp.Transport
{
	/// <summary>
	/// Interface for MCP transport protocols (stdio, SSE, etc.)
	/// </summary>
	public interface ITransportProtocol : IDisposable
	{
		/// <summary>
		/// Event fired when a JSON-RPC message is received
		/// </summary>
		event EventHandler<JsonRpcMessage> MessageReceived;

		/// <summary>
		/// Event fired when a transport error occurs
		/// </summary>
		event EventHandler<Exception> ErrorOccurred;

		/// <summary>
		/// Gets whether the transport is currently connected
		/// </summary>
		bool IsConnected { get; }

		/// <summary>
		/// Connect to the MCP server
		/// </summary>
		Task ConnectAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// Send a JSON-RPC message to the server
		/// </summary>
		Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default);

		/// <summary>
		/// Disconnect from the MCP server
		/// </summary>
		Task DisconnectAsync(CancellationToken cancellationToken = default);
	}
}
