using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JocysCom.VS.AiCompanion.Engine.Mcp.Models;
using JocysCom.VS.AiCompanion.Engine.Mcp.Protocol;
using JocysCom.VS.AiCompanion.Engine.Mcp.Transport;

namespace JocysCom.VS.AiCompanion.Engine.Mcp
{
	/// <summary>
	/// Manages the lifecycle of MCP servers including starting, stopping, monitoring, and restarting
	/// </summary>
	public class McpServerManager : IDisposable
	{
		private readonly ConcurrentDictionary<string, McpServerInstance> _servers;
		private readonly Timer _healthCheckTimer;
		private readonly McpConfigurationLoader _configLoader;
		private readonly object _lockObject = new object();
		private bool _disposed = false;

		public event EventHandler<McpServerStatusChangedEventArgs> ServerStatusChanged;
		public event EventHandler<McpServerErrorEventArgs> ServerError;
		public event EventHandler<McpServersDiscoveredEventArgs> ServersDiscovered;

		public McpServerManager()
		{
			_servers = new ConcurrentDictionary<string, McpServerInstance>();
			_configLoader = new McpConfigurationLoader();
			_healthCheckTimer = new Timer(PerformHealthChecks, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
		}

		/// <summary>
		/// Get all managed server instances
		/// </summary>
		public IReadOnlyCollection<McpServerInstance> GetAllServers()
		{
			return _servers.Values.ToList().AsReadOnly();
		}

		/// <summary>
		/// Get a specific server instance by ID
		/// </summary>
		public McpServerInstance GetServer(string serverId)
		{
			_servers.TryGetValue(serverId, out var server);
			return server;
		}

		/// <summary>
		/// Start an MCP server with the specified configuration
		/// </summary>
		public async Task<McpServerInstance> StartServerAsync(McpServerConfig config, CancellationToken cancellationToken = default)
		{
			if (config == null)
				throw new ArgumentNullException(nameof(config));

			if (string.IsNullOrEmpty(config.ServerId))
				throw new ArgumentException("Server ID cannot be empty", nameof(config));

			lock (_lockObject)
			{
				if (_servers.ContainsKey(config.ServerId))
					throw new InvalidOperationException($"Server with ID '{config.ServerId}' is already registered");
			}

			var instance = new McpServerInstance(config);
			instance.StatusChanged += OnServerStatusChanged;
			instance.ErrorOccurred += OnServerError;

			_servers.TryAdd(config.ServerId, instance);

			try
			{
				await instance.StartAsync(cancellationToken);
				return instance;
			}
			catch (Exception)
			{
				_servers.TryRemove(config.ServerId, out _);
				instance.StatusChanged -= OnServerStatusChanged;
				instance.ErrorOccurred -= OnServerError;
				instance.Dispose();
				throw;
			}
		}

		/// <summary>
		/// Stop an MCP server
		/// </summary>
		public async Task StopServerAsync(string serverId, CancellationToken cancellationToken = default)
		{
			if (!_servers.TryGetValue(serverId, out var instance))
				return;

			try
			{
				await instance.StopAsync(cancellationToken);
			}
			finally
			{
				_servers.TryRemove(serverId, out _);
				instance.StatusChanged -= OnServerStatusChanged;
				instance.ErrorOccurred -= OnServerError;
				instance.Dispose();
			}
		}

		/// <summary>
		/// Restart an MCP server
		/// </summary>
		public async Task<McpServerInstance> RestartServerAsync(string serverId, CancellationToken cancellationToken = default)
		{
			if (!_servers.TryGetValue(serverId, out var instance))
				throw new ArgumentException($"Server with ID '{serverId}' not found");

			var config = instance.Config;

			// Stop the current instance
			await StopServerAsync(serverId, cancellationToken);

			// Start a new instance with the same configuration
			return await StartServerAsync(config, cancellationToken);
		}

		/// <summary>
		/// Stop all managed servers
		/// </summary>
		public async Task StopAllServersAsync(CancellationToken cancellationToken = default)
		{
			var stopTasks = new List<Task>();
			var serverIds = _servers.Keys.ToList();

			foreach (var serverId in serverIds)
			{
				stopTasks.Add(StopServerAsync(serverId, cancellationToken));
			}

			await Task.WhenAll(stopTasks);
		}

		/// <summary>
		/// Get server status information
		/// </summary>
		public McpServerStatus GetServerStatus(string serverId)
		{
			if (!_servers.TryGetValue(serverId, out var instance))
				return McpServerStatus.NotFound;

			return instance.Status;
		}

		/// <summary>
		/// Check if a server is healthy and responsive
		/// </summary>
		public async Task<bool> IsServerHealthyAsync(string serverId, CancellationToken cancellationToken = default)
		{
			if (!_servers.TryGetValue(serverId, out var instance))
				return false;

			return await instance.IsHealthyAsync(cancellationToken);
		}

		/// <summary>
		/// Discover and load MCP servers from standard mcp.json files
		/// </summary>
		/// <param name="basePath">Base path to search from (defaults to current directory)</param>
		/// <returns>List of discovered server configurations</returns>
		public async Task<List<McpServerConfig>> DiscoverServersAsync(string basePath = null)
		{
			var discoveredConfigs = new List<McpServerConfig>();

			try
			{
				// Discover all mcp.json files
				var configurations = await _configLoader.DiscoverConfigurationsAsync(basePath);

				foreach (var configKvp in configurations)
				{
					var configFilePath = configKvp.Key;
					var mcpConfig = configKvp.Value;

					// Convert each server definition to McpServerConfig
					foreach (var serverKvp in mcpConfig.AllServers)
					{
						var serverId = serverKvp.Key;
						var serverDef = serverKvp.Value;

						// Skip disabled servers
						if (serverDef.Disabled)
							continue;

						var config = McpServerConfig.FromServerDefinition(serverId, serverDef, configFilePath);
						discoveredConfigs.Add(config);
					}
				}

				// Fire discovery event
				ServersDiscovered?.Invoke(this, new McpServersDiscoveredEventArgs(discoveredConfigs, configurations.Keys.ToList()));
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error discovering MCP servers: {ex.Message}");
			}

			return discoveredConfigs;
		}

		/// <summary>
		/// Load a specific mcp.json file
		/// </summary>
		/// <param name="filePath">Path to the mcp.json file</param>
		/// <returns>List of server configurations from the file</returns>
		public async Task<List<McpServerConfig>> LoadConfigurationFileAsync(string filePath)
		{
			var configs = new List<McpServerConfig>();

			try
			{
				var mcpConfig = await _configLoader.LoadAsync(filePath);

				foreach (var serverKvp in mcpConfig.AllServers)
				{
					var serverId = serverKvp.Key;
					var serverDef = serverKvp.Value;

					// Skip disabled servers
					if (serverDef.Disabled)
						continue;

					var config = McpServerConfig.FromServerDefinition(serverId, serverDef, filePath);
					configs.Add(config);
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error loading MCP configuration from '{filePath}': {ex.Message}");
				throw;
			}

			return configs;
		}

		/// <summary>
		/// Save server configurations back to mcp.json format
		/// </summary>
		/// <param name="configs">Server configurations to save</param>
		/// <param name="filePath">Path to save the mcp.json file</param>
		public async Task SaveConfigurationFileAsync(List<McpServerConfig> configs, string filePath)
		{
			try
			{
				// Group configs by their original file path
				var configsByFile = configs.GroupBy(c => c.ConfigurationFilePath ?? filePath);

				foreach (var group in configsByFile)
				{
					var targetFilePath = group.Key;
					var mcpConfig = new McpConfiguration();

					// Convert back to standard format
					foreach (var config in group)
					{
						if (!string.IsNullOrEmpty(config.ServerId))
						{
							mcpConfig.McpServers[config.ServerId] = config.ToServerDefinition();
						}
					}

					await _configLoader.SaveAsync(mcpConfig, targetFilePath);
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error saving MCP configuration to '{filePath}': {ex.Message}");
				throw;
			}
		}

		/// <summary>
		/// Create a default mcp.json file with common servers
		/// </summary>
		/// <param name="filePath">Path to create the file</param>
		public async Task CreateDefaultConfigurationAsync(string filePath = "mcp.json")
		{
			var defaultConfig = McpConfigurationLoader.CreateDefaultConfiguration();
			await _configLoader.SaveAsync(defaultConfig, filePath);
		}

		/// <summary>
		/// Initialize the manager by discovering and loading MCP configurations
		/// </summary>
		/// <param name="basePath">Base path to search for configurations</param>
		/// <returns>Number of servers discovered</returns>
		public async Task<int> InitializeAsync(string basePath = null)
		{
			var discoveredConfigs = await DiscoverServersAsync(basePath);

			// Update Global.AppSettings.McpServers with discovered configurations
			// This allows the UI and settings system to manage them
			if (Global.AppSettings?.McpServers != null)
			{
				// Clear existing auto-discovered servers and add new ones
				var existingServers = Global.AppSettings.McpServers.Where(s => string.IsNullOrEmpty(s.ConfigurationFilePath)).ToList();
				foreach (var server in existingServers)
				{
					Global.AppSettings.McpServers.Remove(server);
				}

				// Add discovered servers
				foreach (var config in discoveredConfigs)
				{
					// Check if this server is already configured (by ServerId and file path)
					var existing = Global.AppSettings.McpServers.FirstOrDefault(s =>
						s.ServerId == config.ServerId && s.ConfigurationFilePath == config.ConfigurationFilePath);

					if (existing == null)
					{
						Global.AppSettings.McpServers.Add(config);
					}
					else
					{
						// Update existing with new definition but preserve user settings
						existing.ServerDefinition = config.ServerDefinition;
					}
				}
			}

			return discoveredConfigs.Count;
		}

		/// <summary>
		/// Start all enabled servers that have AutoStart enabled
		/// </summary>
		/// <param name="configs">Server configurations</param>
		/// <returns>Number of servers started successfully</returns>
		public async Task<int> StartAutoStartServersAsync(IEnumerable<McpServerConfig> configs)
		{
			int startedCount = 0;
			var tasks = new List<Task<bool>>();

			foreach (var config in configs)
			{
				if (!config.Disabled && config.AutoStart)
				{
					tasks.Add(StartServerAsyncHelper(config));
				}
			}

			var results = await Task.WhenAll(tasks);
			foreach (bool result in results)
			{
				if (result) startedCount++;
			}

			return startedCount;
		}

		private async Task<bool> StartServerAsyncHelper(McpServerConfig config)
		{
			try
			{
				await StartServerAsync(config);
				return true;
			}
			catch
			{
				return false;
			}
		}

		private void PerformHealthChecks(object state)
		{
			_ = Task.Run(async () =>
			{
				try
				{
					var healthCheckTasks = new List<Task>();

					foreach (var kvp in _servers)
					{
						var serverId = kvp.Key;
						var instance = kvp.Value;

						if (instance.Status == McpServerStatus.Running)
						{
							healthCheckTasks.Add(CheckServerHealthAsync(serverId, instance));
						}
					}

					await Task.WhenAll(healthCheckTasks);
				}
				catch (Exception ex)
				{
					// Log health check errors but don't propagate
					ServerError?.Invoke(this, new McpServerErrorEventArgs("HealthCheck", ex));
				}
			});
		}

		private async Task CheckServerHealthAsync(string serverId, McpServerInstance instance)
		{
			try
			{
				var isHealthy = await instance.IsHealthyAsync(CancellationToken.None);

				if (!isHealthy && instance.Config.AutoRestart)
				{
					// Server is not healthy and auto-restart is enabled
					_ = Task.Run(async () =>
					{
						try
						{
							await RestartServerAsync(serverId, CancellationToken.None);
						}
						catch (Exception ex)
						{
							ServerError?.Invoke(this, new McpServerErrorEventArgs(serverId, ex));
						}
					});
				}
			}
			catch (Exception ex)
			{
				ServerError?.Invoke(this, new McpServerErrorEventArgs(serverId, ex));
			}
		}

		private void OnServerStatusChanged(object sender, McpServerStatusChangedEventArgs e)
		{
			ServerStatusChanged?.Invoke(sender, e);
		}

		private void OnServerError(object sender, McpServerErrorEventArgs e)
		{
			ServerError?.Invoke(sender, e);
		}

		public void Dispose()
		{
			if (_disposed)
				return;

			_disposed = true;

			try
			{
				_healthCheckTimer?.Dispose();

				// Stop all servers
				var stopTask = StopAllServersAsync(CancellationToken.None);
				stopTask.Wait(TimeSpan.FromSeconds(10)); // Give servers time to shutdown gracefully
			}
			catch { }
		}
	}

	/// <summary>
	/// Event arguments for server status changes
	/// </summary>
	public class McpServerStatusChangedEventArgs : EventArgs
	{
		public string ServerId { get; }
		public McpServerStatus OldStatus { get; }
		public McpServerStatus NewStatus { get; }

		public McpServerStatusChangedEventArgs(string serverId, McpServerStatus oldStatus, McpServerStatus newStatus)
		{
			ServerId = serverId;
			OldStatus = oldStatus;
			NewStatus = newStatus;
		}

	}

	/// <summary>
	/// Event arguments for server discovery
	/// </summary>
	public class McpServersDiscoveredEventArgs : EventArgs
	{
		public List<McpServerConfig> Servers { get; }
		public List<string> ConfigurationFiles { get; }

		public McpServersDiscoveredEventArgs(List<McpServerConfig> servers, List<string> configFiles)
		{
			Servers = servers ?? new List<McpServerConfig>();
			ConfigurationFiles = configFiles ?? new List<string>();
		}
	}

	/// <summary>
	/// Event arguments for server errors
	/// </summary>
	public class McpServerErrorEventArgs : EventArgs
	{
		public string ServerId { get; }
		public Exception Exception { get; }

		public McpServerErrorEventArgs(string serverId, Exception exception)
		{
			ServerId = serverId;
			Exception = exception;
		}
	}
}
