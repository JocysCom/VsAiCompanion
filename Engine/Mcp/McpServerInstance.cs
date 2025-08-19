using System;
using System.Threading;
using System.Threading.Tasks;
using JocysCom.VS.AiCompanion.Engine.Mcp.Protocol;
using JocysCom.VS.AiCompanion.Engine.Mcp.Transport;

namespace JocysCom.VS.AiCompanion.Engine.Mcp
{
	/// <summary>
	/// Represents a single MCP server instance with lifecycle management
	/// </summary>
	public class McpServerInstance : IDisposable
	{
		private McpClient _client;
		private ITransportProtocol _transport;
		private McpServerStatus _status;
		private McpServerCapabilities _capabilities;
		private DateTime _lastHealthCheck;
		private readonly object _lockObject = new object();
		private bool _disposed = false;

		public event EventHandler<McpServerStatusChangedEventArgs> StatusChanged;
		public event EventHandler<McpServerErrorEventArgs> ErrorOccurred;

		/// <summary>
		/// Server configuration
		/// </summary>
		public McpServerConfig Config { get; }

		/// <summary>
		/// Current server status
		/// </summary>
		public McpServerStatus Status
		{
			get => _status;
			private set
			{
				if (_status != value)
				{
					var oldStatus = _status;
					_status = value;
					StatusChanged?.Invoke(this, new McpServerStatusChangedEventArgs(Config.ServerId, oldStatus, value));
				}
			}
		}

		/// <summary>
		/// Server capabilities after initialization
		/// </summary>
		public McpServerCapabilities Capabilities => _capabilities;

		/// <summary>
		/// MCP client for communication
		/// </summary>
		public McpClient Client => _client;

		/// <summary>
		/// Time of last successful health check
		/// </summary>
		public DateTime LastHealthCheck => _lastHealthCheck;

		public McpServerInstance(McpServerConfig config)
		{
			Config = config ?? throw new ArgumentNullException(nameof(config));
			_status = McpServerStatus.Stopped;
		}

		/// <summary>
		/// Start the MCP server
		/// </summary>
		public async Task StartAsync(CancellationToken cancellationToken = default)
		{
			lock (_lockObject)
			{
				if (_status != McpServerStatus.Stopped)
					throw new InvalidOperationException($"Server is already {_status}");

				Status = McpServerStatus.Starting;
			}

			try
			{
				// Create appropriate transport based on configuration
				_transport = CreateTransport(Config);

				// Create MCP client
				_client = new McpClient(_transport);
				_client.ErrorOccurred += OnClientError;

				// Connect and initialize
				var clientCapabilities = new McpClientCapabilities();
				_capabilities = await _client.ConnectAsync(clientCapabilities, cancellationToken);

				Status = McpServerStatus.Running;
				_lastHealthCheck = DateTime.UtcNow;
			}
			catch (Exception ex)
			{
				Status = McpServerStatus.Error;
				CleanupResources();
				ErrorOccurred?.Invoke(this, new McpServerErrorEventArgs(Config.ServerId, ex));
				throw;
			}
		}

		/// <summary>
		/// Stop the MCP server
		/// </summary>
		public async Task StopAsync(CancellationToken cancellationToken = default)
		{
			lock (_lockObject)
			{
				if (_status == McpServerStatus.Stopped)
					return;

				Status = McpServerStatus.Stopping;
			}

			try
			{
				if (_transport != null)
				{
					await _transport.DisconnectAsync(cancellationToken);
				}
			}
			catch (Exception ex)
			{
				ErrorOccurred?.Invoke(this, new McpServerErrorEventArgs(Config.ServerId, ex));
			}
			finally
			{
				CleanupResources();
				Status = McpServerStatus.Stopped;
			}
		}

		/// <summary>
		/// Check if the server is healthy and responsive
		/// </summary>
		public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
		{
			if (_status != McpServerStatus.Running || _client == null)
				return false;

			try
			{
				// Try to list tools as a health check
				await _client.ListToolsAsync(cancellationToken);
				_lastHealthCheck = DateTime.UtcNow;
				return true;
			}
			catch (Exception ex)
			{
				ErrorOccurred?.Invoke(this, new McpServerErrorEventArgs(Config.ServerId, ex));
				Status = McpServerStatus.Error;
				return false;
			}
		}

		/// <summary>
		/// Get available tools from the server
		/// </summary>
		public async Task<McpListToolsResult> GetToolsAsync(CancellationToken cancellationToken = default)
		{
			if (_status != McpServerStatus.Running)
				throw new InvalidOperationException("Server is not running");

			return await _client.ListToolsAsync(cancellationToken);
		}

		/// <summary>
		/// Call a tool on the server
		/// </summary>
		public async Task<McpCallToolResult> CallToolAsync(string toolName, object arguments, CancellationToken cancellationToken = default)
		{
			if (_status != McpServerStatus.Running)
				throw new InvalidOperationException("Server is not running");

			return await _client.CallToolAsync(toolName, arguments, cancellationToken);
		}

		/// <summary>
		/// Get available resources from the server
		/// </summary>
		public async Task<McpListResourcesResult> GetResourcesAsync(CancellationToken cancellationToken = default)
		{
			if (_status != McpServerStatus.Running)
				throw new InvalidOperationException("Server is not running");

			return await _client.ListResourcesAsync(cancellationToken);
		}

		/// <summary>
		/// Read a resource from the server
		/// </summary>
		public async Task<McpReadResourceResult> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
		{
			if (_status != McpServerStatus.Running)
				throw new InvalidOperationException("Server is not running");

			return await _client.ReadResourceAsync(uri, cancellationToken);
		}

		private ITransportProtocol CreateTransport(McpServerConfig config)
		{
			// Determine transport type from configuration
			var isStdio = !string.IsNullOrEmpty(config.Command);
			var isSse = !string.IsNullOrEmpty(config.Url);

			if (isStdio && !isSse)
			{
				// Stdio transport
				return new StdioTransport(config.Command, config.Args, config.Cwd);
			}
			else if (isSse && !isStdio)
			{
				// SSE transport
				return new SseTransport(config.Url);
			}
			else if (isStdio && isSse)
			{
				// Both specified - prefer the transport type if specified, otherwise prefer Stdio
				if (!string.IsNullOrEmpty(config.Transport))
				{
					switch (config.Transport.ToLowerInvariant())
					{
						case "sse":
						case "http":
						case "https":
							return new SseTransport(config.Url);
						case "stdio":
						default:
							return new StdioTransport(config.Command, config.Args, config.Cwd);
					}
				}
				else
				{
					return new StdioTransport(config.Command, config.Args, config.Cwd);
				}
			}
			else
			{
				throw new InvalidOperationException($"Server configuration must specify either Command (for stdio) or Url (for SSE transport)");
			}
		}

		private void OnClientError(object sender, Exception ex)
		{
			Status = McpServerStatus.Error;
			ErrorOccurred?.Invoke(this, new McpServerErrorEventArgs(Config.ServerId, ex));
		}

		private void CleanupResources()
		{
			try
			{
				if (_client != null)
				{
					_client.ErrorOccurred -= OnClientError;
					_client.Dispose();
					_client = null;
				}

				_transport?.Dispose();
				_transport = null;
				_capabilities = null;
			}
			catch { }
		}

		public void Dispose()
		{
			if (_disposed)
				return;

			_disposed = true;

			try
			{
				var stopTask = StopAsync(CancellationToken.None);
				stopTask.Wait(TimeSpan.FromSeconds(5));
			}
			catch { }
		}
	}

	/// <summary>
	/// MCP server status enumeration
	/// </summary>
	public enum McpServerStatus
	{
		/// <summary>
		/// Server not found or not registered
		/// </summary>
		NotFound,

		/// <summary>
		/// Server is stopped
		/// </summary>
		Stopped,

		/// <summary>
		/// Server is starting up
		/// </summary>
		Starting,

		/// <summary>
		/// Server is running and operational
		/// </summary>
		Running,

		/// <summary>
		/// Server is stopping
		/// </summary>
		Stopping,

		/// <summary>
		/// Server is in error state
		/// </summary>
		Error
	}

	/// <summary>
	/// MCP transport type enumeration
	/// </summary>
	public enum McpTransportType
	{
		/// <summary>
		/// Standard input/output based transport
		/// </summary>
		Stdio,

		/// <summary>
		/// Server-Sent Events (HTTP) based transport
		/// </summary>
		Sse
	}
}
