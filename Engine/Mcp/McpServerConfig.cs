using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.VS.AiCompanion.Engine;
using JocysCom.VS.AiCompanion.Engine.Mcp.Models;
using JocysCom.VS.AiCompanion.Plugins.Core;

namespace JocysCom.VS.AiCompanion.Engine.Mcp
{
	/// <summary>
	/// MCP Server configuration wrapper that combines standard mcp.json format
	/// with application-specific settings for approval and management
	/// </summary>
	public class McpServerConfig : SettingsListFileItem
	{
		private string _serverId;
		private McpServerDefinition _serverDefinition;
		private bool _autoStart;
		private bool _autoRestart;
		private int _maxRestartAttempts;
		private int _healthCheckIntervalSeconds;
		private RiskLevel _maxRiskLevel;
		private ToolCallApprovalProcess _approvalProcess;
		private string _configurationFilePath;

		/// <summary>
		/// Server identifier (key from mcp.json)
		/// </summary>
		[DefaultValue(null)]
		public string ServerId
		{
			get => _serverId;
			set => SetProperty(ref _serverId, value);
		}

		/// <summary>
		/// Standard MCP server definition (from mcp.json)
		/// </summary>
		[XmlIgnore, JsonIgnore]
		public McpServerDefinition ServerDefinition
		{
			get => _serverDefinition ?? (_serverDefinition = new McpServerDefinition());
			set => SetProperty(ref _serverDefinition, value);
		}

		/// <summary>
		/// Path to the mcp.json file containing this server configuration
		/// </summary>
		[DefaultValue(null)]
		public string ConfigurationFilePath
		{
			get => _configurationFilePath;
			set => SetProperty(ref _configurationFilePath, value);
		}

		/// <summary>
		/// Whether to start the server automatically when the application starts
		/// (Application-specific setting)
		/// </summary>
		[DefaultValue(false)]
		public bool AutoStart
		{
			get => _autoStart;
			set => SetProperty(ref _autoStart, value);
		}

		/// <summary>
		/// Whether to restart the server automatically if it fails
		/// (Application-specific setting)
		/// </summary>
		[DefaultValue(true)]
		public bool AutoRestart
		{
			get => _autoRestart;
			set => SetProperty(ref _autoRestart, value);
		}

		/// <summary>
		/// Maximum number of restart attempts before giving up
		/// (Application-specific setting)
		/// </summary>
		[DefaultValue(3)]
		public int MaxRestartAttempts
		{
			get => _maxRestartAttempts;
			set => SetProperty(ref _maxRestartAttempts, value);
		}

		/// <summary>
		/// Health check interval in seconds
		/// (Application-specific setting)
		/// </summary>
		[DefaultValue(30)]
		public int HealthCheckIntervalSeconds
		{
			get => _healthCheckIntervalSeconds;
			set => SetProperty(ref _healthCheckIntervalSeconds, value);
		}

		/// <summary>
		/// Maximum risk level allowed for tools from this server
		/// (Application-specific setting)
		/// </summary>
		[DefaultValue(RiskLevel.Medium)]
		public RiskLevel MaxRiskLevel
		{
			get => _maxRiskLevel;
			set => SetProperty(ref _maxRiskLevel, value);
		}

		/// <summary>
		/// Approval process for tool calls from this server
		/// (Application-specific setting)
		/// </summary>
		[DefaultValue(ToolCallApprovalProcess.User)]
		public ToolCallApprovalProcess ApprovalProcess
		{
			get => _approvalProcess;
			set => SetProperty(ref _approvalProcess, value);
		}

		// Convenience properties that map to ServerDefinition

		/// <summary>
		/// Command to execute (from mcp.json)
		/// </summary>
		[XmlIgnore, JsonIgnore]
		public string Command
		{
			get => ServerDefinition?.Command;
			set { if (ServerDefinition != null) ServerDefinition.Command = value; OnPropertyChanged(); }
		}

		/// <summary>
		/// Command arguments (from mcp.json)
		/// </summary>
		[XmlIgnore, JsonIgnore]
		public string[] Args
		{
			get => ServerDefinition?.Args;
			set { if (ServerDefinition != null) ServerDefinition.Args = value; OnPropertyChanged(); }
		}

		/// <summary>
		/// Environment variables (from mcp.json)
		/// </summary>
		[XmlIgnore, JsonIgnore]
		public Dictionary<string, string> Env
		{
			get => ServerDefinition?.Env;
			set { if (ServerDefinition != null) ServerDefinition.Env = value; OnPropertyChanged(); }
		}

		/// <summary>
		/// Working directory (from mcp.json)
		/// </summary>
		[XmlIgnore, JsonIgnore]
		public string Cwd
		{
			get => ServerDefinition?.Cwd;
			set { if (ServerDefinition != null) ServerDefinition.Cwd = value; OnPropertyChanged(); }
		}

		/// <summary>
		/// Server description (from mcp.json)
		/// </summary>
		[XmlIgnore, JsonIgnore]
		public string ServerDescription
		{
			get => ServerDefinition?.Description;
			set { if (ServerDefinition != null) ServerDefinition.Description = value; OnPropertyChanged(); }
		}

		/// <summary>
		/// Whether the server is disabled in mcp.json
		/// </summary>
		[XmlIgnore, JsonIgnore]
		public bool Disabled
		{
			get => ServerDefinition?.Disabled ?? false;
			set { if (ServerDefinition != null) ServerDefinition.Disabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(Enabled)); }
		}

		/// <summary>
		/// Whether the server is enabled (inverse of Disabled)
		/// </summary>
		[XmlIgnore, JsonIgnore]
		public bool Enabled
		{
			get => !Disabled;
			set { Disabled = !value; OnPropertyChanged(); }
		}

		/// <summary>
		/// Base URL for SSE transport (from mcp.json)
		/// </summary>
		[XmlIgnore, JsonIgnore]
		public string Url
		{
			get => ServerDefinition?.Url;
			set { if (ServerDefinition != null) ServerDefinition.Url = value; OnPropertyChanged(); }
		}

		/// <summary>
		/// Transport type override (from mcp.json)
		/// </summary>
		[XmlIgnore, JsonIgnore]
		public string Transport
		{
			get => ServerDefinition?.Transport;
			set { if (ServerDefinition != null) ServerDefinition.Transport = value; OnPropertyChanged(); }
		}

		public McpServerConfig() : base()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
		}

		/// <summary>
		/// Create McpServerConfig from standard mcp.json server definition
		/// </summary>
		/// <param name="serverId">Server identifier from mcp.json</param>
		/// <param name="definition">Standard server definition</param>
		/// <param name="configFilePath">Path to the mcp.json file</param>
		/// <returns>McpServerConfig instance</returns>
		public static McpServerConfig FromServerDefinition(string serverId, McpServerDefinition definition, string configFilePath = null)
		{
			var config = new McpServerConfig
			{
				ServerId = serverId,
				Name = serverId, // Use server ID as display name
				ServerDefinition = definition,
				ConfigurationFilePath = configFilePath
			};

			// Use description from mcp.json if available
			if (!string.IsNullOrEmpty(definition?.Description))
			{
				config.Name = definition.Description;
			}

			return config;
		}

		/// <summary>
		/// Convert back to standard MCP server definition
		/// </summary>
		/// <returns>Standard server definition for mcp.json</returns>
		public McpServerDefinition ToServerDefinition()
		{
			return ServerDefinition ?? new McpServerDefinition();
		}

		/// <summary>
		/// Validate the configuration
		/// </summary>
		public bool IsValid(out string errorMessage)
		{
			if (string.IsNullOrEmpty(ServerId))
			{
				errorMessage = "Server ID cannot be empty";
				return false;
			}

			if (ServerDefinition == null)
			{
				errorMessage = "Server definition cannot be null";
				return false;
			}

			if (string.IsNullOrEmpty(ServerDefinition.Command) && string.IsNullOrEmpty(ServerDefinition.Url))
			{
				errorMessage = "Either Command or URL must be specified";
				return false;
			}

			if (!string.IsNullOrEmpty(ServerDefinition.Url))
			{
				if (!Uri.TryCreate(ServerDefinition.Url, UriKind.Absolute, out _))
				{
					errorMessage = "URL is not a valid URI";
					return false;
				}
			}

			if (MaxRestartAttempts < 0)
			{
				errorMessage = "Max restart attempts cannot be negative";
				return false;
			}

			if (HealthCheckIntervalSeconds < 1)
			{
				errorMessage = "Health check interval must be at least 1 second";
				return false;
			}

			errorMessage = null;
			return true;
		}

		/// <summary>
		/// Create a copy of this configuration
		/// </summary>
		public McpServerConfig Clone()
		{
			var clone = new McpServerConfig
			{
				ServerId = this.ServerId,
				Name = this.Name,
				Path = this.Path,
				ConfigurationFilePath = this.ConfigurationFilePath,
				AutoStart = this.AutoStart,
				AutoRestart = this.AutoRestart,
				MaxRestartAttempts = this.MaxRestartAttempts,
				HealthCheckIntervalSeconds = this.HealthCheckIntervalSeconds,
				MaxRiskLevel = this.MaxRiskLevel,
				ApprovalProcess = this.ApprovalProcess
			};

			// Deep copy server definition
			if (this.ServerDefinition != null)
			{
				clone.ServerDefinition = new McpServerDefinition
				{
					Command = this.ServerDefinition.Command,
					Args = this.ServerDefinition.Args?.ToArray(),
					Cwd = this.ServerDefinition.Cwd,
					Description = this.ServerDefinition.Description,
					Disabled = this.ServerDefinition.Disabled,
					Transport = this.ServerDefinition.Transport,
					Url = this.ServerDefinition.Url,
					Env = this.ServerDefinition.Env != null ? new Dictionary<string, string>(this.ServerDefinition.Env) : null
				};
			}

			return clone;
		}

		/// <summary>
		/// Check if this configuration is empty
		/// </summary>
		public override bool IsEmpty =>
			string.IsNullOrEmpty(ServerId) &&
			(ServerDefinition == null ||
			 (string.IsNullOrEmpty(ServerDefinition.Command) && string.IsNullOrEmpty(ServerDefinition.Url)));
	}
}
