# Qdrant MCP Server Analysis

## Introduction

The [Qdrant MCP Server](https://github.com/qdrant/mcp-server-qdrant) is an official implementation of the Model Context Protocol (MCP) server for the Qdrant vector search engine. This server acts as a semantic memory layer on top of the Qdrant database, enabling AI applications to store and retrieve information using vector embeddings.

## What is the Model Context Protocol (MCP)?

The [Model Context Protocol (MCP)](https://modelcontextprotocol.github.io/) is an open protocol that enables seamless integration between LLM applications and external data sources and tools. It provides a standardized way to connect LLMs with the context they need, whether you're:

- Building an AI-powered IDE
- Enhancing a chat interface
- Creating custom AI workflows

MCP establishes a common interface for LLMs to interact with external systems, making it easier to extend AI capabilities with specialized tools and data sources.

## Qdrant MCP Server Features

This MCP server implementation allows you to:

1. Store code snippets, documentation, and implementation details in Qdrant
2. Retrieve relevant code examples based on semantic search
3. Help developers find specific implementations or usage patterns
4. Provide a semantic memory layer for AI applications

## Tools Provided

The Qdrant MCP server exposes two main tools:

1. **qdrant-store**
   - Stores information in the Qdrant database
   - Input: `information` (string) - Information to store
   - Returns: Confirmation message

2. **qdrant-find**
   - Retrieves relevant information from the Qdrant database
   - Input: `query` (string) - Query to use for searching
   - Returns: Information stored in the Qdrant database as separate messages

## Environment Variables

The server is configured using the following environment variables:

| Name | Description | Default Value |
|------|-------------|---------------|
| QDRANT_URL | URL of the Qdrant server | None |
| QDRANT_API_KEY | API key for the Qdrant server | None |
| QDRANT_LOCAL_PATH | Path to the local Qdrant database | None |
| COLLECTION_NAME | Name of the collection to use | Required |
| EMBEDDING_PROVIDER | Embedding provider to use (currently only "fastembed" is supported) | fastembed |
| EMBEDDING_MODEL | Name of the embedding model to use | sentence-transformers/all-MiniLM-L6-v2 |
| TOOL_STORE_DESCRIPTION | Custom description for the store tool | See default in settings.py |
| TOOL_FIND_DESCRIPTION | Custom description for the find tool | See default in settings.py |

**Note:** You cannot provide both `QDRANT_URL` and `QDRANT_LOCAL_PATH` at the same time.

## Installation Methods

### Using UVX

When using [uvx](https://github.com/astral-sh/uv), no specific installation is needed to directly run mcp-server-qdrant:

```bash
QDRANT_URL="http://localhost:6333" \
COLLECTION_NAME="my-collection" \
EMBEDDING_MODEL="sentence-transformers/all-MiniLM-L6-v2" \
uvx mcp-server-qdrant
```

### Using Docker

A Dockerfile is available for building and running the MCP server:

```bash
# Build the container
docker build -t mcp-server-qdrant .

# Run the container
docker run -p 8000:8000 \
  -e QDRANT_URL="http://your-qdrant-server:6333" \
  -e QDRANT_API_KEY="your-api-key" \
  -e COLLECTION_NAME="your-collection" \
  mcp-server-qdrant
```

### Installing via Smithery

To install Qdrant MCP Server for Claude Desktop automatically via [Smithery](https://github.com/smithery-io/smithery-cli):

```bash
npx @smithery/cli install mcp-server-qdrant --client claude
```

## Transport Protocols

The server supports different transport protocols that can be specified using the `--transport` flag:

- `sse`: Server-Sent Events transport, perfect for remote clients
- `stdio`: Standard input/output (default if not specified)

Example:
```bash
QDRANT_URL="http://localhost:6333" \
COLLECTION_NAME="my-collection" \
uvx mcp-server-qdrant --transport sse
```

## Integration with AI Tools

### Manual Configuration for Claude Desktop

To use this server with the Claude Desktop app, add the following configuration to the "mcpServers" section of your `claude_desktop_config.json`:

```json
{
  "qdrant": {
    "command": "uvx",
    "args": ["mcp-server-qdrant"],
    "env": {
      "QDRANT_URL": "https://xyz-example.eu-central.aws.cloud.qdrant.io",
      "QDRANT_API_KEY": "your_api_key",
      "COLLECTION_NAME": "your-collection-name",
      "EMBEDDING_MODEL": "sentence-transformers/all-MiniLM-L6-v2"
    }
  }
}
```

For local Qdrant mode:

```json
{
  "qdrant": {
    "command": "uvx",
    "args": ["mcp-server-qdrant"],
    "env": {
      "QDRANT_LOCAL_PATH": "/path/to/local/qdrant/storage",
      "COLLECTION_NAME": "your-collection-name",
      "EMBEDDING_MODEL": "sentence-transformers/all-MiniLM-L6-v2"
    }
  }
}
```

The MCP server will automatically create a collection with the specified name if it doesn't exist.

### Using with Cursor/Windsurf

You can configure this MCP server to work as a code search tool for Cursor or Windsurf by customizing the tool descriptions:

```bash
QDRANT_URL="http://localhost:6333" \
COLLECTION_NAME="my-collection" \
TOOL_STORE_DESCRIPTION="Store code snippets with descriptions. The 'information' parameter should contain a natural language description while the actual code should be included in the 'metadata' parameter. The value of 'metadata' is a Python dictionary with strings as keys. Use this whenever you generate some code snippet." \
TOOL_FIND_DESCRIPTION="Search for relevant code snippets based on natural language description. The 'query' parameter should describe what you're looking for, and the tool will return the most relevant code snippets. Use this when you need to find existing code snippets for reuse." \
uvx mcp-server-qdrant --transport sse # Enable SSE transport
```

In Cursor/Windsurf, you can then configure the MCP server in your settings by pointing to this running server using SSE transport protocol. The description on how to add an MCP server to Cursor can be found in the [Cursor documentation](https://cursor.sh/docs/mcp).

If you are running Cursor/Windsurf locally, you can use the following URL:

```
http://localhost:8000/sse
```

**Tip:** SSE transport is recommended as a preferred way to connect Cursor/Windsurf to the MCP server, as it can support remote connections, making it easy to share the server with your team or use it in a cloud environment.

## Using Semantic Code Search in Claude Code

Tool descriptions, specified in `TOOL_STORE_DESCRIPTION` and `TOOL_FIND_DESCRIPTION`, should be customized for your specific use case. However, Claude Code should be already able to:

1. Use the `qdrant-store` tool to store code snippets with descriptions
2. Use the `qdrant-find` tool to search for relevant code snippets using natural language

## Testing and Debugging

The [MCP inspector](https://github.com/modelcontextprotocol/inspector) is a developer tool for testing and debugging MCP servers. It runs both a client UI (default port 5173) and an MCP proxy server (default port 3000). Open the client UI in your browser to use the inspector.

```bash
QDRANT_URL=":memory:" COLLECTION_NAME="test" \
mcp dev src/mcp_server_qdrant/server.py
```

Once started, open your browser to http://localhost:5173 to access the inspector interface.

## License

This MCP server is licensed under the Apache License 2.0, which means you are free to use, modify, and distribute the software, subject to the terms and conditions of the Apache License 2.0.

## Contributing

If you have suggestions for how mcp-server-qdrant could be improved, or want to report a bug, open an issue on the GitHub repository. Contributions are welcome!
