using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JocysCom.VS.AiCompanion.Engine.Mcp.Protocol
{
	/// <summary>
	/// MCP client capabilities
	/// </summary>
	public class McpClientCapabilities
	{
		[JsonPropertyName("experimental")]
		public Dictionary<string, object> Experimental { get; set; } = new Dictionary<string, object>();

		[JsonPropertyName("sampling")]
		public object Sampling { get; set; }
	}

	/// <summary>
	/// MCP server capabilities
	/// </summary>
	public class McpServerCapabilities
	{
		[JsonPropertyName("experimental")]
		public Dictionary<string, object> Experimental { get; set; } = new Dictionary<string, object>();

		[JsonPropertyName("logging")]
		public object Logging { get; set; }

		[JsonPropertyName("prompts")]
		public McpPromptsCapability Prompts { get; set; }

		[JsonPropertyName("resources")]
		public McpResourcesCapability Resources { get; set; }

		[JsonPropertyName("tools")]
		public McpToolsCapability Tools { get; set; }
	}

	/// <summary>
	/// MCP initialize result
	/// </summary>
	public class McpInitializeResult
	{
		[JsonPropertyName("protocolVersion")]
		public string ProtocolVersion { get; set; }

		[JsonPropertyName("capabilities")]
		public McpServerCapabilities Capabilities { get; set; }

		[JsonPropertyName("serverInfo")]
		public McpServerInfo ServerInfo { get; set; }

		[JsonPropertyName("instructions")]
		public string Instructions { get; set; }
	}

	/// <summary>
	/// MCP server info
	/// </summary>
	public class McpServerInfo
	{
		[JsonPropertyName("name")]
		public string Name { get; set; }

		[JsonPropertyName("version")]
		public string Version { get; set; }
	}

	/// <summary>
	/// MCP tools capability
	/// </summary>
	public class McpToolsCapability
	{
		[JsonPropertyName("listChanged")]
		public bool? ListChanged { get; set; }
	}

	/// <summary>
	/// MCP resources capability
	/// </summary>
	public class McpResourcesCapability
	{
		[JsonPropertyName("subscribe")]
		public bool? Subscribe { get; set; }

		[JsonPropertyName("listChanged")]
		public bool? ListChanged { get; set; }
	}

	/// <summary>
	/// MCP prompts capability
	/// </summary>
	public class McpPromptsCapability
	{
		[JsonPropertyName("listChanged")]
		public bool? ListChanged { get; set; }
	}

	/// <summary>
	/// MCP tool definition
	/// </summary>
	public class McpTool
	{
		[JsonPropertyName("name")]
		public string Name { get; set; }

		[JsonPropertyName("description")]
		public string Description { get; set; }

		[JsonPropertyName("inputSchema")]
		public McpToolInputSchema InputSchema { get; set; }
	}

	/// <summary>
	/// MCP tool input schema
	/// </summary>
	public class McpToolInputSchema
	{
		[JsonPropertyName("type")]
		public string Type { get; set; }

		[JsonPropertyName("properties")]
		public Dictionary<string, object> Properties { get; set; }

		[JsonPropertyName("required")]
		public List<string> Required { get; set; }

		[JsonPropertyName("additionalProperties")]
		public bool? AdditionalProperties { get; set; }

		[JsonPropertyName("$schema")]
		public string Schema { get; set; }
	}

	/// <summary>
	/// MCP list tools result
	/// </summary>
	public class McpListToolsResult
	{
		[JsonPropertyName("tools")]
		public List<McpTool> Tools { get; set; } = new List<McpTool>();
	}

	/// <summary>
	/// MCP tool call result
	/// </summary>
	public class McpCallToolResult
	{
		[JsonPropertyName("content")]
		public List<McpContent> Content { get; set; } = new List<McpContent>();

		[JsonPropertyName("isError")]
		public bool? IsError { get; set; }
	}

	/// <summary>
	/// MCP content item
	/// </summary>
	public class McpContent
	{
		[JsonPropertyName("type")]
		public string Type { get; set; }

		[JsonPropertyName("text")]
		public string Text { get; set; }

		[JsonPropertyName("data")]
		public string Data { get; set; }

		[JsonPropertyName("mimeType")]
		public string MimeType { get; set; }
	}

	/// <summary>
	/// MCP resource definition
	/// </summary>
	public class McpResource
	{
		[JsonPropertyName("uri")]
		public string Uri { get; set; }

		[JsonPropertyName("name")]
		public string Name { get; set; }

		[JsonPropertyName("description")]
		public string Description { get; set; }

		[JsonPropertyName("mimeType")]
		public string MimeType { get; set; }
	}

	/// <summary>
	/// MCP list resources result
	/// </summary>
	public class McpListResourcesResult
	{
		[JsonPropertyName("resources")]
		public List<McpResource> Resources { get; set; } = new List<McpResource>();
	}

	/// <summary>
	/// MCP read resource result
	/// </summary>
	public class McpReadResourceResult
	{
		[JsonPropertyName("contents")]
		public List<McpResourceContents> Contents { get; set; } = new List<McpResourceContents>();
	}

	/// <summary>
	/// MCP resource contents
	/// </summary>
	public class McpResourceContents
	{
		[JsonPropertyName("uri")]
		public string Uri { get; set; }

		[JsonPropertyName("mimeType")]
		public string MimeType { get; set; }

		[JsonPropertyName("text")]
		public string Text { get; set; }

		[JsonPropertyName("blob")]
		public string Blob { get; set; }
	}
}
