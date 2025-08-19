using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace JocysCom.VS.AiCompanion.Engine.Mcp.Models
{
	/// <summary>
	/// Standard MCP configuration format compatible with all AI agents (Copilot, Cline, Roo Code, etc.)
	/// Represents the root structure of mcp.json
	/// </summary>
	public class McpConfiguration
	{
		/// <summary>
		/// MCP servers configuration - standard format
		/// </summary>
		[JsonPropertyName("mcpServers")]
		public Dictionary<string, McpServerDefinition> McpServers { get; set; } = new Dictionary<string, McpServerDefinition>();

		/// <summary>
		/// Alternative servers property name (some tools use "servers" instead of "mcpServers")
		/// </summary>
		[JsonPropertyName("servers")]
		public Dictionary<string, McpServerDefinition> Servers { get; set; } = new Dictionary<string, McpServerDefinition>();

		/// <summary>
		/// Get all servers from both mcpServers and servers properties
		/// </summary>
		[JsonIgnore]
		public Dictionary<string, McpServerDefinition> AllServers
		{
			get
			{
				var result = new Dictionary<string, McpServerDefinition>();

				// Add servers from mcpServers
				if (McpServers != null)
				{
					foreach (var kvp in McpServers)
					{
						result[kvp.Key] = kvp.Value;
					}
				}

				// Add servers from servers (if not already present)
				if (Servers != null)
				{
					foreach (var kvp in Servers)
					{
						if (!result.ContainsKey(kvp.Key))
						{
							result[kvp.Key] = kvp.Value;
						}
					}
				}

				return result;
			}
		}
	}

	/// <summary>
	/// Standard MCP server definition format
	/// </summary>
	public class McpServerDefinition
	{
		/// <summary>
		/// Command to execute (e.g., "npx", "python", "node")
		/// </summary>
		[JsonPropertyName("command")]
		public string Command { get; set; }

		/// <summary>
		/// Arguments to pass to the command
		/// </summary>
		[JsonPropertyName("args")]
		public string[] Args { get; set; } = new string[0];

		/// <summary>
		/// Environment variables to set
		/// </summary>
		[JsonPropertyName("env")]
		public Dictionary<string, string> Env { get; set; } = new Dictionary<string, string>();

		/// <summary>
		/// Working directory for the command (optional)
		/// </summary>
		[JsonPropertyName("cwd")]
		public string Cwd { get; set; }

		/// <summary>
		/// Whether the server is disabled (optional, defaults to false)
		/// </summary>
		[JsonPropertyName("disabled")]
		public bool Disabled { get; set; } = false;

		/// <summary>
		/// Server description (optional, extension for UI purposes)
		/// </summary>
		[JsonPropertyName("description")]
		public string Description { get; set; }

		/// <summary>
		/// Transport type override (optional, extension - usually auto-detected)
		/// </summary>
		[JsonPropertyName("transport")]
		public string Transport { get; set; }

		/// <summary>
		/// Base URL for SSE transport (if applicable)
		/// </summary>
		[JsonPropertyName("url")]
		public string Url { get; set; }
	}
}
