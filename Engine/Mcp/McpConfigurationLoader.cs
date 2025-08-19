using JocysCom.VS.AiCompanion.Engine.Mcp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine.Mcp
{
	/// <summary>
	/// Loads and manages standard mcp.json configuration files
	/// Compatible with Copilot, Cline, Roo Code, and other AI agents
	/// </summary>
	public class McpConfigurationLoader
	{
		/// <summary>
		/// Standard locations to search for mcp.json files
		/// </summary>
		public static readonly string[] StandardConfigPaths = new[]
		{
			"mcp.json",                           // Current directory
			".mcp/mcp.json",                      // Hidden .mcp directory
			"~/.config/mcp/mcp.json",            // User config directory (Linux/Mac)
			"%APPDATA%/mcp/mcp.json",            // User config directory (Windows)
			"%USERPROFILE%/.config/mcp/mcp.json" // Alternative Windows path
		};

		private readonly JsonSerializerOptions _jsonOptions;

		public McpConfigurationLoader()
		{
			_jsonOptions = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true,
				AllowTrailingCommas = true,
				ReadCommentHandling = JsonCommentHandling.Skip,
				WriteIndented = true
			};
		}

		/// <summary>
		/// Load MCP configuration from a file
		/// </summary>
		/// <param name="filePath">Path to the mcp.json file</param>
		/// <returns>Parsed MCP configuration</returns>
		public async Task<McpConfiguration> LoadAsync(string filePath)
		{
			if (!File.Exists(filePath))
				throw new FileNotFoundException($"MCP configuration file not found: {filePath}");

			try
			{
				var json = await Task.Run(() => File.ReadAllText(filePath));
				var config = JsonSerializer.Deserialize<McpConfiguration>(json, _jsonOptions);

				// Validate configuration
				ValidateConfiguration(config, filePath);

				return config;
			}
			catch (JsonException ex)
			{
				throw new InvalidOperationException($"Invalid JSON in MCP configuration file '{filePath}': {ex.Message}", ex);
			}
		}

		/// <summary>
		/// Save MCP configuration to a file
		/// </summary>
		/// <param name="config">Configuration to save</param>
		/// <param name="filePath">Path to save the file</param>
		public async Task SaveAsync(McpConfiguration config, string filePath)
		{
			try
			{
				// Ensure directory exists
				var directory = Path.GetDirectoryName(filePath);
				if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
				{
					Directory.CreateDirectory(directory);
				}

				var json = JsonSerializer.Serialize(config, _jsonOptions);
				await Task.Run(() => File.WriteAllText(filePath, json));
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException($"Failed to save MCP configuration to '{filePath}': {ex.Message}", ex);
			}
		}

		/// <summary>
		/// Discover and load all mcp.json files from standard locations
		/// </summary>
		/// <param name="basePath">Base path to search from (defaults to current directory)</param>
		/// <returns>Dictionary of found configurations with their file paths</returns>
		public async Task<Dictionary<string, McpConfiguration>> DiscoverConfigurationsAsync(string basePath = null)
		{
			basePath = basePath ?? Environment.CurrentDirectory;
			var configurations = new Dictionary<string, McpConfiguration>();

			// Search standard locations
			foreach (var relativePath in StandardConfigPaths)
			{
				var fullPath = ResolvePath(relativePath, basePath);

				if (File.Exists(fullPath))
				{
					try
					{
						var config = await LoadAsync(fullPath);
						configurations[fullPath] = config;
					}
					catch (Exception ex)
					{
						// Log warning but continue with other files
						Console.WriteLine($"Warning: Failed to load MCP configuration from '{fullPath}': {ex.Message}");
					}
				}
			}

			// Also search for any mcp.json files in subdirectories
			await SearchDirectoryAsync(basePath, configurations, maxDepth: 3);

			return configurations;
		}

		/// <summary>
		/// Create a default mcp.json configuration with common servers
		/// </summary>
		/// <returns>Default MCP configuration</returns>
		public static McpConfiguration CreateDefaultConfiguration()
		{
			return new McpConfiguration
			{
				McpServers = new Dictionary<string, McpServerDefinition>
				{
					["filesystem"] = new McpServerDefinition
					{
						Command = "npx",
						Args = new[] { "-y", "@modelcontextprotocol/server-filesystem", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) },
						Description = "Local filesystem access"
					},
					["brave-search"] = new McpServerDefinition
					{
						Command = "npx",
						Args = new[] { "-y", "@modelcontextprotocol/server-brave-search" },
						Env = new Dictionary<string, string>
						{
							["BRAVE_API_KEY"] = "your-brave-api-key-here"
						},
						Description = "Brave search engine access",
						Disabled = true // Disabled by default until API key is configured
					},
					["github"] = new McpServerDefinition
					{
						Command = "npx",
						Args = new[] { "-y", "@modelcontextprotocol/server-github" },
						Env = new Dictionary<string, string>
						{
							["GITHUB_PERSONAL_ACCESS_TOKEN"] = "your-github-token-here"
						},
						Description = "GitHub repository access",
						Disabled = true // Disabled by default until token is configured
					}
				}
			};
		}

		/// <summary>
		/// Validate MCP configuration for common issues
		/// </summary>
		/// <param name="config">Configuration to validate</param>
		/// <param name="filePath">File path for error reporting</param>
		private void ValidateConfiguration(McpConfiguration config, string filePath)
		{
			if (config == null)
				throw new InvalidOperationException($"MCP configuration is null in file '{filePath}'");

			var allServers = config.AllServers;
			if (allServers == null || allServers.Count == 0)
				throw new InvalidOperationException($"No servers defined in MCP configuration '{filePath}'");

			foreach (var kvp in allServers)
			{
				var serverName = kvp.Key;
				var server = kvp.Value;

				if (string.IsNullOrEmpty(serverName))
					throw new InvalidOperationException($"Server name cannot be empty in MCP configuration '{filePath}'");

				if (server == null)
					throw new InvalidOperationException($"Server definition for '{serverName}' is null in MCP configuration '{filePath}'");

				if (string.IsNullOrEmpty(server.Command))
					throw new InvalidOperationException($"Command is required for server '{serverName}' in MCP configuration '{filePath}'");
			}
		}

		/// <summary>
		/// Resolve path with environment variables and relative paths
		/// </summary>
		/// <param name="path">Path to resolve</param>
		/// <param name="basePath">Base path for relative resolution</param>
		/// <returns>Resolved absolute path</returns>
		private string ResolvePath(string path, string basePath)
		{
			// Expand environment variables
			path = Environment.ExpandEnvironmentVariables(path);

			// Handle ~ for home directory
			if (path.StartsWith("~/"))
			{
				path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path.Substring(2));
			}

			// Make absolute if relative
			if (!Path.IsPathRooted(path))
			{
				path = Path.Combine(basePath, path);
			}

			return Path.GetFullPath(path);
		}

		/// <summary>
		/// Recursively search directory for mcp.json files
		/// </summary>
		/// <param name="directory">Directory to search</param>
		/// <param name="configurations">Dictionary to add found configurations to</param>
		/// <param name="currentDepth">Current search depth</param>
		/// <param name="maxDepth">Maximum search depth</param>
		private async Task SearchDirectoryAsync(string directory, Dictionary<string, McpConfiguration> configurations, int currentDepth = 0, int maxDepth = 3)
		{
			if (currentDepth >= maxDepth)
				return;

			try
			{
				// Look for mcp.json in current directory
				var mcpJsonPath = Path.Combine(directory, "mcp.json");
				if (File.Exists(mcpJsonPath) && !configurations.ContainsKey(mcpJsonPath))
				{
					try
					{
						var config = await LoadAsync(mcpJsonPath);
						configurations[mcpJsonPath] = config;
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Warning: Failed to load MCP configuration from '{mcpJsonPath}': {ex.Message}");
					}
				}

				// Search subdirectories
				var subdirectories = Directory.GetDirectories(directory);
				foreach (var subdirectory in subdirectories)
				{
					// Skip hidden directories and common ignore patterns
					var dirName = Path.GetFileName(subdirectory);
					if (dirName.StartsWith(".") && dirName != ".mcp")
						continue;
					if (dirName.Equals("node_modules", StringComparison.OrdinalIgnoreCase))
						continue;
					if (dirName.Equals("bin", StringComparison.OrdinalIgnoreCase))
						continue;
					if (dirName.Equals("obj", StringComparison.OrdinalIgnoreCase))
						continue;

					await SearchDirectoryAsync(subdirectory, configurations, currentDepth + 1, maxDepth);
				}
			}
			catch (UnauthorizedAccessException)
			{
				// Skip directories we can't access
			}
			catch (DirectoryNotFoundException)
			{
				// Skip if directory doesn't exist
			}
		}
	}
}
