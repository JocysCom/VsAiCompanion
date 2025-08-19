using JocysCom.VS.AiCompanion.Engine.Mcp.Protocol;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine.Mcp.Transport
{
	/// <summary>
	/// SSE (Server-Sent Events) based transport for HTTP-based MCP servers
	/// </summary>
	public class SseTransport : ITransportProtocol
	{
		private readonly string _baseUrl;
		private readonly HttpClient _httpClient;
		private readonly CancellationTokenSource _cancellationTokenSource;
		private Task _readTask;
		private bool _disposed = false;

		public event EventHandler<JsonRpcMessage> MessageReceived;
		public event EventHandler<Exception> ErrorOccurred;

		public bool IsConnected { get; private set; }

		public SseTransport(string baseUrl, HttpClient httpClient = null)
		{
			_baseUrl = baseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseUrl));
			_httpClient = httpClient ?? new HttpClient();
			_cancellationTokenSource = new CancellationTokenSource();
		}

		public async Task ConnectAsync(CancellationToken cancellationToken = default)
		{
			if (IsConnected)
				return;

			try
			{
				// Test connection by making a simple request
				var response = await _httpClient.GetAsync($"{_baseUrl}/health", cancellationToken);
				response.EnsureSuccessStatusCode();

				IsConnected = true;

				// Start SSE connection for receiving messages
				_readTask = Task.Run(() => ReadSseStreamAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
			}
			catch (Exception ex)
			{
				IsConnected = false;
				if (ex is McpException)
					throw;
				throw new McpException($"Failed to connect to SSE MCP server: {ex.Message}", ex);
			}
		}

		public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
		{
			if (!IsConnected)
				throw new McpException("Transport is not connected");

			try
			{
				var json = JsonHelper.Serialize(message);
				var content = new StringContent(json, Encoding.UTF8, "application/json");

				var response = await _httpClient.PostAsync($"{_baseUrl}/message", content, cancellationToken);
				response.EnsureSuccessStatusCode();
			}
			catch (Exception ex)
			{
				ErrorOccurred?.Invoke(this, ex);
				throw new McpException($"Failed to send message: {ex.Message}", ex);
			}
		}

		public async Task DisconnectAsync(CancellationToken cancellationToken = default)
		{
			if (!IsConnected)
				return;

			try
			{
				IsConnected = false;
				_cancellationTokenSource.Cancel();

				if (_readTask != null)
				{
					await _readTask;
				}
			}
			catch (Exception ex)
			{
				ErrorOccurred?.Invoke(this, ex);
			}
		}

		private async Task ReadSseStreamAsync(CancellationToken cancellationToken)
		{
			try
			{
				var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/sse");
				request.Headers.Add("Accept", "text/event-stream");
				request.Headers.Add("Cache-Control", "no-cache");

				var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
				response.EnsureSuccessStatusCode();

				var stream = await response.Content.ReadAsStreamAsync();
				var reader = new StreamReader(stream);

				string line;
				var eventBuilder = new StringBuilder();
				string eventType = null;

				while ((line = await reader.ReadLineAsync()) != null && !cancellationToken.IsCancellationRequested)
				{
					if (string.IsNullOrEmpty(line))
					{
						// Empty line indicates end of event
						if (eventBuilder.Length > 0)
						{
							ProcessSseEvent(eventType, eventBuilder.ToString());
							eventBuilder.Clear();
							eventType = null;
						}
						continue;
					}

					if (line.StartsWith("event:"))
					{
						eventType = line.Substring(6).Trim();
					}
					else if (line.StartsWith("data:"))
					{
						var data = line.Substring(5).Trim();
						if (eventBuilder.Length > 0)
							eventBuilder.AppendLine();
						eventBuilder.Append(data);
					}
					// Ignore other SSE fields like id:, retry:, etc.
				}
			}
			catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
			{
				IsConnected = false;
				ErrorOccurred?.Invoke(this, ex);
			}
		}

		private void ProcessSseEvent(string eventType, string data)
		{
			try
			{
				if (eventType == "message" || eventType == null)
				{
					var message = JsonHelper.ParseMessage(data);
					if (message != null)
					{
						MessageReceived?.Invoke(this, message);
					}
				}
				// Handle other event types if needed
			}
			catch (Exception ex)
			{
				ErrorOccurred?.Invoke(this, new McpException($"Failed to process SSE event: {data}", ex));
			}
		}

		public void Dispose()
		{
			if (_disposed)
				return;

			_disposed = true;

			try
			{
				IsConnected = false;
				_cancellationTokenSource?.Cancel();
				_readTask?.Wait(TimeSpan.FromSeconds(1));
				_cancellationTokenSource?.Dispose();
				_httpClient?.Dispose();
			}
			catch { }
		}
	}
}
