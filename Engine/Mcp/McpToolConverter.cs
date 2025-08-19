using JocysCom.VS.AiCompanion.Engine.Mcp.Protocol;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;

namespace JocysCom.VS.AiCompanion.Engine.Mcp
{
	/// <summary>
	/// Converts MCP tool definitions to internal PluginItem format for approval and execution
	/// </summary>
	public static class McpToolConverter
	{
		/// <summary>
		/// Convert an MCP tool to a PluginItem for integration with existing plugin system
		/// </summary>
		public static PluginItem ConvertToPluginItem(McpTool mcpTool, McpServerConfig serverConfig, string serverId)
		{
			if (mcpTool == null)
				throw new ArgumentNullException(nameof(mcpTool));
			if (serverConfig == null)
				throw new ArgumentNullException(nameof(serverConfig));

			var pluginItem = new PluginItem
			{
				Id = $"MCP.{serverId}.{mcpTool.Name}",
				Name = mcpTool.Name,
				Description = mcpTool.Description ?? $"MCP tool from {serverConfig.Name}",
				Class = serverConfig.Name,
				ClassFullName = $"MCP.{serverId}",
				Namespace = "JocysCom.VS.AiCompanion.Engine.Mcp",
				AssemblyName = "MCP Server Tools",
				RiskLevel = DetermineRiskLevel(mcpTool, serverConfig),
				IsEnabled = !serverConfig.Disabled,
				Params = new BindingList<PluginParam>()
			};

			// Set icon based on risk level (this will be handled by PluginItem's SetIconBasedOnRiskLevel)
			pluginItem.GetType().GetMethod("SetIconBasedOnRiskLevel",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
				?.Invoke(pluginItem, null);

			// Convert input schema to parameters
			if (mcpTool.InputSchema?.Properties != null)
			{
				ConvertSchemaToParams(mcpTool.InputSchema, pluginItem.Params);
			}

			return pluginItem;
		}

		/// <summary>
		/// Convert a list of MCP tools to PluginItems
		/// </summary>
		public static List<PluginItem> ConvertToPluginItems(IEnumerable<McpTool> mcpTools, McpServerConfig serverConfig, string serverId)
		{
			if (mcpTools == null)
				return new List<PluginItem>();

			return mcpTools.Select(tool => ConvertToPluginItem(tool, serverConfig, serverId)).ToList();
		}

		/// <summary>
		/// Convert MCP tool input schema to PluginParam objects
		/// </summary>
		private static void ConvertSchemaToParams(McpToolInputSchema schema, BindingList<PluginParam> parameters)
		{
			if (schema.Properties == null)
				return;

			var index = 0;
			var requiredProperties = schema.Required ?? new List<string>();

			foreach (var property in schema.Properties)
			{
				var param = new PluginParam
				{
					Name = property.Key,
					Index = index++,
					IsOptional = !requiredProperties.Contains(property.Key)
				};

				// Extract property details from the schema
				if (property.Value is JsonElement jsonElement)
				{
					ExtractParamDetailsFromJsonElement(param, jsonElement);
				}
				else if (property.Value is Dictionary<string, object> propDict)
				{
					ExtractParamDetailsFromDictionary(param, propDict);
				}
				else
				{
					// Fallback - treat as string
					param.Type = "string";
					param.Description = "Parameter for MCP tool";
				}

				parameters.Add(param);
			}
		}

		/// <summary>
		/// Extract parameter details from JsonElement
		/// </summary>
		private static void ExtractParamDetailsFromJsonElement(PluginParam param, JsonElement element)
		{
			// Get type
			if (element.TryGetProperty("type", out var typeElement))
			{
				param.Type = MapJsonSchemaTypeToPluginType(typeElement.GetString());
			}
			else
			{
				param.Type = "object";
			}

			// Get description
			if (element.TryGetProperty("description", out var descElement))
			{
				param.Description = descElement.GetString();
			}

			// Handle array types
			if (param.Type == "array" && element.TryGetProperty("items", out var itemsElement))
			{
				if (itemsElement.TryGetProperty("type", out var itemTypeElement))
				{
					param.Type = $"{MapJsonSchemaTypeToPluginType(itemTypeElement.GetString())}[]";
				}
			}

			// Handle object types with additional properties
			if (param.Type == "object")
			{
				if (element.TryGetProperty("properties", out _))
				{
					param.Type = "object"; // Complex object
				}
				else if (element.TryGetProperty("additionalProperties", out var additionalPropsElement))
				{
					if (additionalPropsElement.ValueKind == JsonValueKind.True)
					{
						param.Type = "Dictionary<string, object>";
					}
				}
			}
		}

		/// <summary>
		/// Extract parameter details from dictionary (fallback method)
		/// </summary>
		private static void ExtractParamDetailsFromDictionary(PluginParam param, Dictionary<string, object> propDict)
		{
			// Get type
			if (propDict.TryGetValue("type", out var typeObj))
			{
				param.Type = MapJsonSchemaTypeToPluginType(typeObj?.ToString());
			}
			else
			{
				param.Type = "object";
			}

			// Get description
			if (propDict.TryGetValue("description", out var descObj))
			{
				param.Description = descObj?.ToString();
			}

			// Handle array types
			if (param.Type == "array" && propDict.TryGetValue("items", out var itemsObj))
			{
				if (itemsObj is Dictionary<string, object> itemsDict && itemsDict.TryGetValue("type", out var itemTypeObj))
				{
					param.Type = $"{MapJsonSchemaTypeToPluginType(itemTypeObj?.ToString())}[]";
				}
			}
		}

		/// <summary>
		/// Map JSON Schema types to C# plugin parameter types
		/// </summary>
		private static string MapJsonSchemaTypeToPluginType(string jsonSchemaType)
		{
			switch (jsonSchemaType?.ToLowerInvariant())
			{
				case "string": return "string";
				case "integer": return "int";
				case "number": return "double";
				case "boolean": return "bool";
				case "array": return "array";
				case "object": return "object";
				case "null": return "object";
				default: return "object";
			}
			;
		}

		/// <summary>
		/// Determine risk level for an MCP tool based on tool characteristics and server configuration
		/// </summary>
		private static Plugins.Core.RiskLevel DetermineRiskLevel(McpTool mcpTool, McpServerConfig serverConfig)
		{
			// Start with server's maximum risk level as the ceiling
			var maxAllowedRisk = serverConfig.MaxRiskLevel;

			// Analyze tool to determine inherent risk level
			var inherentRisk = AnalyzeToolRisk(mcpTool);

			// Return the lower of the two (more restrictive)
			return (Plugins.Core.RiskLevel)Math.Min((int)inherentRisk, (int)maxAllowedRisk);
		}

		/// <summary>
		/// Analyze MCP tool characteristics to determine inherent risk level
		/// </summary>
		private static Plugins.Core.RiskLevel AnalyzeToolRisk(McpTool mcpTool)
		{
			var toolName = mcpTool.Name?.ToLowerInvariant() ?? "";
			var description = mcpTool.Description?.ToLowerInvariant() ?? "";

			// High-risk indicators
			var highRiskKeywords = new[]
			{
				"execute", "run", "command", "shell", "powershell", "cmd",
				"delete", "remove", "destroy", "kill", "terminate",
				"write", "create", "modify", "update", "install", "uninstall",
				"network", "http", "request", "download", "upload",
				"file", "directory", "folder", "path"
			};

			// Critical risk indicators
			var criticalRiskKeywords = new[]
			{
				"system", "admin", "root", "sudo", "elevated",
				"registry", "service", "process", "kernel",
				"database", "sql", "inject", "exploit"
			};

			// Medium risk indicators
			var mediumRiskKeywords = new[]
			{
				"search", "find", "query", "filter", "sort",
				"parse", "format", "convert", "transform",
				"analyze", "calculate", "compute"
			};

			// Check for critical risk first
			if (criticalRiskKeywords.Any(keyword => toolName.Contains(keyword) || description.Contains(keyword)))
			{
				return Plugins.Core.RiskLevel.Critical;
			}

			// Check for high risk
			if (highRiskKeywords.Any(keyword => toolName.Contains(keyword) || description.Contains(keyword)))
			{
				return Plugins.Core.RiskLevel.High;
			}

			// Check for medium risk
			if (mediumRiskKeywords.Any(keyword => toolName.Contains(keyword) || description.Contains(keyword)))
			{
				return Plugins.Core.RiskLevel.Medium;
			}

			// Check if tool has parameters that might indicate risk
			if (mcpTool.InputSchema?.Properties?.Any() == true)
			{
				var hasFileParams = mcpTool.InputSchema.Properties.Keys.Any(key =>
					key.ToLowerInvariant().Contains("file") ||
					key.ToLowerInvariant().Contains("path") ||
					key.ToLowerInvariant().Contains("directory"));

				var hasCommandParams = mcpTool.InputSchema.Properties.Keys.Any(key =>
					key.ToLowerInvariant().Contains("command") ||
					key.ToLowerInvariant().Contains("script") ||
					key.ToLowerInvariant().Contains("execute"));

				if (hasCommandParams)
					return Plugins.Core.RiskLevel.High;
				if (hasFileParams)
					return Plugins.Core.RiskLevel.Medium;

				// Tools with parameters are at least low risk
				return Plugins.Core.RiskLevel.Low;
			}

			// Default for simple read-only tools
			return Plugins.Core.RiskLevel.None;
		}

		/// <summary>
		/// Create arguments object from PluginItem parameters for MCP tool execution
		/// </summary>
		public static object CreateArgumentsFromPluginItem(PluginItem pluginItem)
		{
			if (pluginItem?.Params == null || !pluginItem.Params.Any())
				return new { };

			var arguments = new Dictionary<string, object>();

			foreach (var param in pluginItem.Params)
			{
				if (param.ParamValue != null)
				{
					arguments[param.Name] = ConvertParamValue(param.ParamValue, param.Type);
				}
				else if (!param.IsOptional)
				{
					// Required parameter missing
					throw new ArgumentException($"Required parameter '{param.Name}' is missing");
				}
			}

			return arguments;
		}

		/// <summary>
		/// Convert parameter value to appropriate type
		/// </summary>
		private static object ConvertParamValue(object value, string targetType)
		{
			if (value == null)
				return null;

			try
			{
				switch (targetType?.ToLowerInvariant())
				{
					case "string": return value.ToString();
					case "int": return Convert.ToInt32(value);
					case "double": return Convert.ToDouble(value);
					case "bool": return Convert.ToBoolean(value);
					case "array": return value is Array arr ? arr : new[] { value };
					default: return value;
				}
			}
			catch
			{
				// If conversion fails, return as string
				return value.ToString();
			}
		}
	}
}
