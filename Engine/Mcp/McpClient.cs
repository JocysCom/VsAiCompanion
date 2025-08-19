using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JocysCom.VS.AiCompanion.Engine.Mcp.Protocol;
using JocysCom.VS.AiCompanion.Engine.Mcp.Transport;

namespace JocysCom.VS.AiCompanion.Engine.Mcp
{
	/// <summary>
	/// MCP (Model Context Protocol) client for communicating with MCP servers
	/// </summary>
	public class McpClient : IDisposable
	{
		private readonly ITransportProtocol _transport;
		private readonly SemaphoreSlim _requestSemaphore;
		private readonly Dictionary<string, TaskCompletionSource<JsonRpcResponse>> _pendingRequests;
		private readonly object _lockObject = new object();
		private int _nextRequestId = 1;
		private bool _disposed = false;

		public event EventHandler<McpServerCapabilities> ServerCapabilitiesReceived;
		public event EventHandler<JsonRpcNotification> NotificationReceived;
		public event EventHandler<Exception> ErrorOccurred;

		public McpClient(ITransportProtocol transport)
		{
			_transport = transport ?? throw new ArgumentNullException(nameof(transport));
			_requestSemaphore = new SemaphoreSlim(1, 1);
			_pendingRequests = new Dictionary<string, TaskCompletionSource<JsonRpcResponse>>();

			_transport.MessageReceived += OnMessageReceived;
			_transport.ErrorOccurred += OnTransportError;
		}

		/// <summary>
		/// Connect to the MCP server and perform initialization handshake
		/// </summary>
		public async Task<McpServerCapabilities> ConnectAsync(McpClientCapabilities clientCapabilities, CancellationToken cancellationToken = default)
		{
			await _transport.ConnectAsync(cancellationToken);

			// Send initialize request
			var initializeRequest = new JsonRpcRequest
			{
				Id = GetNextRequestId(),
				Method = "initialize",
				Params = new
				{
					protocolVersion = "2024-11-05",
					capabilities = clientCapabilities,
					clientInfo = new
					{
						name = "JocysCom AI Companion",
						version = "1.17.23"
					}
				}
			};

			var response = await SendRequestAsync(initializeRequest, cancellationToken);
			if (response.Error != null)
			{
				throw new McpException($"Initialize failed: {response.Error.Message}");
			}

			var serverCapabilities = JsonHelper.Deserialize<McpInitializeResult>(response.Result);

			// Send initialized notification
			await SendNotificationAsync("initialized", null);

			ServerCapabilitiesReceived?.Invoke(this, serverCapabilities.Capabilities);
			return serverCapabilities.Capabilities;
		}

		/// <summary>
		/// Get list of available tools from the server
		/// </summary>
		public async Task<McpListToolsResult> ListToolsAsync(CancellationToken cancellationToken = default)
		{
			var request = new JsonRpcRequest
			{
				Id = GetNextRequestId(),
				Method = "tools/list",
				Params = new { }
			};

			var response = await SendRequestAsync(request, cancellationToken);
			if (response.Error != null)
			{
				throw new McpException($"List tools failed: {response.Error.Message}");
			}

			return JsonHelper.Deserialize<McpListToolsResult>(response.Result);
		}

		/// <summary>
		/// Call a tool on the server
		/// </summary>
		public async Task<McpCallToolResult> CallToolAsync(string toolName, object arguments, CancellationToken cancellationToken = default)
		{
			var request = new JsonRpcRequest
			{
				Id = GetNextRequestId(),
				Method = "tools/call",
				Params = new
				{
					name = toolName,
					arguments = arguments
				}
			};

			var response = await SendRequestAsync(request, cancellationToken);
			if (response.Error != null)
			{
				throw new McpException($"Tool call failed: {response.Error.Message}");
			}

			return JsonHelper.Deserialize<McpCallToolResult>(response.Result);
		}

		/// <summary>
		/// Get list of available resources from the server
		/// </summary>
		public async Task<McpListResourcesResult> ListResourcesAsync(CancellationToken cancellationToken = default)
		{
			var request = new JsonRpcRequest
			{
				Id = GetNextRequestId(),
				Method = "resources/list",
				Params = new { }
			};

			var response = await SendRequestAsync(request, cancellationToken);
			if (response.Error != null)
			{
				throw new McpException($"List resources failed: {response.Error.Message}");
			}

			return JsonHelper.Deserialize<McpListResourcesResult>(response.Result);
		}

		/// <summary>
		/// Read a resource from the server
		/// </summary>
		public async Task<McpReadResourceResult> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
		{
			var request = new JsonRpcRequest
			{
				Id = GetNextRequestId(),
				Method = "resources/read",
				Params = new
				{
					uri = uri
				}
			};

			var response = await SendRequestAsync(request, cancellationToken);
			if (response.Error != null)
			{
				throw new McpException($"Read resource failed: {response.Error.Message}");
			}

			return JsonHelper.Deserialize<McpReadResourceResult>(response.Result);
		}

		private async Task<JsonRpcResponse> SendRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken)
		{
			var tcs = new TaskCompletionSource<JsonRpcResponse>();

			lock (_lockObject)
			{
				_pendingRequests[request.Id] = tcs;
			}

			try
			{
				await _requestSemaphore.WaitAsync(cancellationToken);
				try
				{
					await _transport.SendMessageAsync(request, cancellationToken);
				}
				finally
				{
					_requestSemaphore.Release();
				}

				return await tcs.Task;
			}
			finally
			{
				lock (_lockObject)
				{
					_pendingRequests.Remove(request.Id);
				}
			}
		}

		private async Task SendNotificationAsync(string method, object parameters)
		{
			var notification = new JsonRpcNotification
			{
				Method = method,
				Params = parameters
			};

			await _requestSemaphore.WaitAsync();
			try
			{
				await _transport.SendMessageAsync(notification, CancellationToken.None);
			}
			finally
			{
				_requestSemaphore.Release();
			}
		}

		private void OnMessageReceived(object sender, JsonRpcMessage message)
		{
			try
			{
				if (message is JsonRpcResponse response)
				{
					TaskCompletionSource<JsonRpcResponse> tcs = null;
					lock (_lockObject)
					{
						if (_pendingRequests.TryGetValue(response.Id, out tcs))
						{
							_pendingRequests.Remove(response.Id);
						}
					}

					tcs?.SetResult(response);
				}
				else if (message is JsonRpcNotification notification)
				{
					NotificationReceived?.Invoke(this, notification);
				}
			}
			catch (Exception ex)
			{
				ErrorOccurred?.Invoke(this, ex);
			}
		}

		private void OnTransportError(object sender, Exception error)
		{
			ErrorOccurred?.Invoke(this, error);
		}

		private string GetNextRequestId()
		{
			return Interlocked.Increment(ref _nextRequestId).ToString();
		}

		public void Dispose()
		{
			if (!_disposed)
			{
				_transport?.Dispose();
				_requestSemaphore?.Dispose();
				_disposed = true;
			}
		}
	}
}
