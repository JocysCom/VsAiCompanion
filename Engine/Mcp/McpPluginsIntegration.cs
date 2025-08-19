using JocysCom.VS.AiCompanion.Engine.Mcp.Protocol;
using JocysCom.VS.AiCompanion.Plugins.Core;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine.Mcp
{
	/// <summary>
	/// Integrates MCP servers into the AI Companion's plugin system while preserving MCP's native design
	/// This class acts as a bridge between AI models and MCP servers, handling approval workflows
	/// </summary>
	public class McpPluginsIntegration
	{
		private readonly McpServerManager _serverManager;

		public McpPluginsIntegration(McpServerManager serverManager)
		{
			_serverManager = serverManager ?? throw new ArgumentNullException(nameof(serverManager));
		}

		/// <summary>
		/// Get MCP tools as ChatTool definitions for AI models (Microsoft's format)
		/// This preserves MCP's native tool calling while integrating with approval system
		/// </summary>
		public async Task<List<ChatTool>> GetMcpChatToolsAsync(TemplateItem item, CancellationToken cancellationToken = default)
		{
			var tools = new List<ChatTool>();

			if (!item.PluginsEnabled)
				return tools;

			var runningServers = _serverManager.GetAllServers()
				.Where(s => s.Status == McpServerStatus.Running && !s.Config.Disabled)
				.ToList();

			foreach (var server in runningServers)
			{
				try
				{
					// Check if server's risk level is acceptable
					var maxRiskLevel = (RiskLevel)Math.Min((int)item.MaxRiskLevel, (int)AppHelper.GetMaxRiskLevel());
					if (server.Config.MaxRiskLevel > maxRiskLevel)
						continue;

					// Get tools from MCP server
					var mcpToolsResult = await server.GetToolsAsync(cancellationToken);

					foreach (var mcpTool in mcpToolsResult.Tools)
					{
						// Create ChatTool that preserves MCP context
						var chatTool = CreateChatToolFromMcpTool(mcpTool, server);
						tools.Add(chatTool);
					}
				}
				catch (Exception ex)
				{
					// Log error but continue with other servers
					System.Diagnostics.Debug.WriteLine($"Error getting tools from MCP server {server.Config.Name}: {ex.Message}");
				}
			}

			return tools;
		}

		/// <summary>
		/// Process MCP tool call with approval workflow integration
		/// This maintains MCP's native calling mechanism while applying existing approval controls
		/// </summary>
		public async Task<(string type, string content)?> ProcessMcpToolCallAsync(
			TemplateItem item,
			ChatToolCall toolCall,
			CancellationTokenSource cancellationTokenSource)
		{
			if (!item.PluginsEnabled)
				return null;

			// Parse MCP server and tool from function name (format: "mcp_{serverId}_{toolName}")
			if (!TryParseMcpToolCall(toolCall.FunctionName, out string serverId, out string toolName))
				return null;

			var server = _serverManager.GetServer(serverId);
			if (server?.Status != McpServerStatus.Running)
				return ("text", $"MCP server '{serverId}' is not available");

			// Check approval process
			if (item.PluginApprovalProcess == ToolCallApprovalProcess.DenyAll)
				return ("text", Resources.MainResources.main_Call_function_request_denied);

			// Create approval item for MCP tool call
			var approvalItem = CreateMcpApprovalItem(server, toolName, toolCall);

			// Apply approval workflow (similar to existing plugin approval)
			var approved = await ApproveExecution(item, approvalItem, cancellationTokenSource);
			if (!approved)
			{
				var messageToAI = Companions.ClientHelper.JoinMessageParts(
					string.IsNullOrEmpty(approvalItem.ApprovalReason)
						? null
						: $"Request Denial Comments: {approvalItem.ApprovalReason}",
					Resources.MainResources.main_Call_function_request_denied);
				return ("text", messageToAI);
			}

			try
			{
				// Execute MCP tool call directly (preserving MCP protocol)
				var arguments = ParseToolArguments(toolCall.FunctionArguments);
				var result = await server.CallToolAsync(toolName, arguments, cancellationTokenSource.Token);

				// Convert MCP result to appropriate response format
				return ConvertMcpResultToResponse(result);
			}
			catch (Exception ex)
			{
				return ("text", $"MCP tool execution failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Get MCP resources as context for AI models
		/// This allows AI models to access MCP resources while maintaining approval controls
		/// </summary>
		public async Task<List<McpResourceInfo>> GetMcpResourcesAsync(TemplateItem item, CancellationToken cancellationToken = default)
		{
			var resources = new List<McpResourceInfo>();

			if (!item.PluginsEnabled)
				return resources;

			var runningServers = _serverManager.GetAllServers()
				.Where(s => s.Status == McpServerStatus.Running && !s.Config.Disabled)
				.ToList();

			foreach (var server in runningServers)
			{
				try
				{
					var maxRiskLevel = (RiskLevel)Math.Min((int)item.MaxRiskLevel, (int)AppHelper.GetMaxRiskLevel());
					if (server.Config.MaxRiskLevel > maxRiskLevel)
						continue;

					var resourcesResult = await server.GetResourcesAsync(cancellationToken);

					foreach (var resource in resourcesResult.Resources)
					{
						resources.Add(new McpResourceInfo
						{
							ServerId = server.Config.ServerId,
							ServerName = server.Config.Name,
							Resource = resource
						});
					}
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"Error getting resources from MCP server {server.Config.Name}: {ex.Message}");
				}
			}

			return resources;
		}

		private ChatTool CreateChatToolFromMcpTool(McpTool mcpTool, McpServerInstance server)
		{
			// Create function name that includes server context
			var functionName = $"mcp_{server.Config.ServerId}_{mcpTool.Name}";

			// Convert MCP input schema to BinaryData for ChatTool
			var parametersJson = JsonHelper.Serialize(new
			{
				type = "object",
				properties = mcpTool.InputSchema?.Properties ?? new Dictionary<string, object>(),
				required = mcpTool.InputSchema?.Required ?? new List<string>(),
				additionalProperties = mcpTool.InputSchema?.AdditionalProperties ?? false
			});

			var binaryParameters = BinaryData.FromString(parametersJson);

			// Enhanced description with server context
			var description = $"[MCP Server: {server.Config.Name}] {mcpTool.Description ?? mcpTool.Name}";

			return ChatTool.CreateFunctionTool(functionName, description, binaryParameters);
		}

		private bool TryParseMcpToolCall(string functionName, out string serverId, out string toolName)
		{
			serverId = null;
			toolName = null;

			if (string.IsNullOrEmpty(functionName) || !functionName.StartsWith("mcp_"))
				return false;

			var parts = functionName.Split('_');
			if (parts.Length < 3)
				return false;

			serverId = parts[1];
			toolName = string.Join("_", parts.Skip(2));
			return true;
		}

		private McpApprovalItem CreateMcpApprovalItem(McpServerInstance server, string toolName, ChatToolCall toolCall)
		{
			var approvalItem = new McpApprovalItem
			{
				ServerId = server.Config.ServerId,
				ServerName = server.Config.Name,
				ToolName = toolName,
				Arguments = ParseToolArguments(toolCall.FunctionArguments),
				RiskLevel = server.Config.MaxRiskLevel,
				ApprovalProcess = server.Config.ApprovalProcess
			};

			return approvalItem;
		}

		private object ParseToolArguments(BinaryData functionArguments)
		{
			if (functionArguments == null)
				return new { };

			try
			{
				return JsonHelper.ParseMessage(functionArguments.ToString());
			}
			catch
			{
				return new { };
			}
		}

		private (string type, string content) ConvertMcpResultToResponse(McpCallToolResult result)
		{
			if (result.IsError == true)
			{
				var errorContent = result.Content?.FirstOrDefault()?.Text ?? "MCP tool execution failed";
				return ("text", errorContent);
			}

			if (result.Content?.Any() == true)
			{
				var content = result.Content.First();
				switch (content.Type)
				{
					case "text": return ("text", content.Text ?? "");
					case "json": return ("json", content.Text ?? "{}");
					default: return ("text", content.Text ?? "");
				}
			}

			return ("text", "MCP tool completed successfully");
		}

		private async Task<bool> ApproveExecution(TemplateItem item, McpApprovalItem approvalItem, CancellationTokenSource cancellationTokenSource)
		{
			// Use the same approval logic as existing plugins but adapted for MCP
			if (item.PluginApprovalProcess == ToolCallApprovalProcess.DenyAll)
				return false;

			if (item.PluginApprovalProcess == ToolCallApprovalProcess.AllowAll)
				return true;

			// For now, use a simplified approval process
			// In a full implementation, this would integrate with the existing PluginApprovalItem UI
			// and follow the same approval workflow as regular plugins

			// TODO: Implement full approval UI integration
			return item.PluginApprovalProcess == ToolCallApprovalProcess.AllowAll;
		}
	}

	/// <summary>
	/// MCP-specific approval item for integration with approval workflow
	/// </summary>
	public class McpApprovalItem
	{
		public string ServerId { get; set; }
		public string ServerName { get; set; }
		public string ToolName { get; set; }
		public object Arguments { get; set; }
		public RiskLevel RiskLevel { get; set; }
		public ToolCallApprovalProcess ApprovalProcess { get; set; }
		public string ApprovalReason { get; set; }
		public bool? IsApproved { get; set; }
		public SemaphoreSlim Semaphore { get; set; } = new SemaphoreSlim(0, 1);
	}

	/// <summary>
	/// Information about an MCP resource
	/// </summary>
	public class McpResourceInfo
	{
		public string ServerId { get; set; }
		public string ServerName { get; set; }
		public McpResource Resource { get; set; }
	}
}
