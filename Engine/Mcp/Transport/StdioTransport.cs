using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JocysCom.VS.AiCompanion.Engine.Mcp.Protocol;

namespace JocysCom.VS.AiCompanion.Engine.Mcp.Transport
{
	/// <summary>
	/// Stdio-based transport for MCP servers that communicate via standard input/output
	/// </summary>
	public class StdioTransport : ITransportProtocol
	{
		private readonly string _command;
		private readonly string[] _arguments;
		private readonly string _workingDirectory;
		private Process _process;
		private StreamWriter _stdin;
		private StreamReader _stdout;
		private readonly CancellationTokenSource _cancellationTokenSource;
		private Task _readTask;
		private bool _disposed = false;

		public event EventHandler<JsonRpcMessage> MessageReceived;
		public event EventHandler<Exception> ErrorOccurred;

		public bool IsConnected => _process != null && !_process.HasExited;

		public StdioTransport(string command, string[] arguments = null, string workingDirectory = null)
		{
			_command = command ?? throw new ArgumentNullException(nameof(command));
			_arguments = arguments ?? new string[0];
			_workingDirectory = workingDirectory;
			_cancellationTokenSource = new CancellationTokenSource();
		}

		public async Task ConnectAsync(CancellationToken cancellationToken = default)
		{
			if (IsConnected)
				return;

			try
			{
				var startInfo = new ProcessStartInfo
				{
					FileName = _command,
					Arguments = string.Join(" ", _arguments),
					UseShellExecute = false,
					RedirectStandardInput = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true,
					WorkingDirectory = _workingDirectory ?? Environment.CurrentDirectory
				};

				_process = Process.Start(startInfo);
				if (_process == null)
					throw new McpException($"Failed to start MCP server process: {_command}");

				_stdin = _process.StandardInput;
				_stdout = _process.StandardOutput;

				// Start reading messages from stdout
				_readTask = Task.Run(ReadMessagesAsync, _cancellationTokenSource.Token);

				// Wait a bit to ensure the process has started properly
				await Task.Delay(100, cancellationToken);

				if (_process.HasExited)
				{
					var exitCode = _process.ExitCode;
					var errorOutput = await _process.StandardError.ReadToEndAsync();
					throw new McpException($"MCP server process exited immediately with code {exitCode}. Error: {errorOutput}");
				}
			}
			catch (Exception ex)
			{
				Dispose();
				if (ex is McpException)
					throw;
				throw new McpException($"Failed to connect to MCP server: {ex.Message}", ex);
			}
		}

		public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
		{
			if (!IsConnected)
				throw new McpException("Transport is not connected");

			try
			{
				var json = JsonHelper.Serialize(message);
				await _stdin.WriteLineAsync(json);
				await _stdin.FlushAsync();
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
				_cancellationTokenSource.Cancel();

				// Close stdin to signal the server to shutdown
				_stdin?.Close();

				// Wait for the process to exit gracefully
				if (_process != null && !_process.HasExited)
				{
					var timeout = TimeSpan.FromSeconds(5);
					if (!_process.WaitForExit((int)timeout.TotalMilliseconds))
					{
						// Force kill if it doesn't exit gracefully
						_process.Kill();
					}
				}

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

		private async Task ReadMessagesAsync()
		{
			try
			{
				string line;
				while ((line = await _stdout.ReadLineAsync()) != null && !_cancellationTokenSource.Token.IsCancellationRequested)
				{
					if (string.IsNullOrWhiteSpace(line))
						continue;

					try
					{
						var message = JsonHelper.ParseMessage(line);
						if (message != null)
						{
							MessageReceived?.Invoke(this, message);
						}
					}
					catch (Exception ex)
					{
						ErrorOccurred?.Invoke(this, new McpException($"Failed to parse message: {line}", ex));
					}
				}
			}
			catch (Exception ex) when (!_cancellationTokenSource.Token.IsCancellationRequested)
			{
				ErrorOccurred?.Invoke(this, ex);
			}
		}

		public void Dispose()
		{
			if (_disposed)
				return;

			_disposed = true;

			try
			{
				_cancellationTokenSource?.Cancel();
				_stdin?.Close();
				_stdout?.Close();

				if (_process != null)
				{
					if (!_process.HasExited)
					{
						try
						{
							_process.Kill();
						}
						catch { }
					}
					_process.Dispose();
				}

				_readTask?.Wait(TimeSpan.FromSeconds(1));
				_cancellationTokenSource?.Dispose();
			}
			catch { }
		}
	}
}
