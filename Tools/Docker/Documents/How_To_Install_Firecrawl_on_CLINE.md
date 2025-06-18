# How to Install Firecrawl MCP Server on Cline

This guide walks you through installing and configuring the Firecrawl MCP server for use with Cline, providing powerful web scraping and crawling capabilities.

## Overview

The Firecrawl MCP server integrates with [Firecrawl](https://github.com/mendableai/firecrawl) to provide:

-   Web scraping and crawling
-   Content extraction and search
-   Deep research capabilities
-   Batch processing with rate limiting
-   Structured data extraction

## Prerequisites

-   Node.js installed on your system
-   A Firecrawl API key (free tier available)
-   Cline extension installed in VS Code

## Step 1: Firecrawl Instance Setup

### Option A: Self-Hosted Firecrawl (Recommended for Local Development)

If you have a self-hosted Firecrawl instance running locally (like on port 3002), you can skip the API key requirement and use your local instance directly. This is ideal for development and testing.

### Option B: Cloud Firecrawl API

1. Visit [https://www.firecrawl.dev/app/api-keys](https://www.firecrawl.dev/app/api-keys)
2. Create an account or sign in if you already have one
3. Generate a new API key
4. Copy the API key (it should start with "fc-")
5. Keep this key secure - you'll need it for configuration

## Step 2: Create MCP Server Directory

Create a dedicated directory for the Firecrawl MCP server:

```cmd
mkdir "C:\Users\%USERNAME%\Documents\Cline\MCP\firecrawl-mcp-server"
```

## Step 3: Install Firecrawl MCP Server

Install the Firecrawl MCP server globally using npm:

```cmd
npm install -g firecrawl-mcp
```

This will install the server globally and make it available system-wide.

## Step 4: Locate Installation Path

Find where npm installed the package:

```cmd
npm config get prefix
```

The firecrawl-mcp executable will typically be located at:

-   `%APPDATA%\npm\node_modules\firecrawl-mcp\build\index.js` (Windows)
-   Or check with: `npm list -g firecrawl-mcp`

## Step 5: Configure Cline MCP Settings

1. Open the Cline MCP settings file:

    ```
    C:\Users\%USERNAME%\AppData\Roaming\Code\User\globalStorage\saoudrizwan.claude-dev\settings\cline_mcp_settings.json
    ```

2. Add the Firecrawl MCP server configuration to the existing `mcpServers` object:

### For Self-Hosted Firecrawl (Local Instance):

```json
{
    "mcpServers": {
        "github.com/mendableai/firecrawl-mcp-server": {
            "autoApprove": [],
            "disabled": false,
            "timeout": 60,
            "type": "stdio",
            "command": "npx",
            "args": ["-y", "firecrawl-mcp"],
            "env": {
                "FIRECRAWL_API_URL": "http://localhost:3002"
            }
        }
    }
}
```

### For Cloud Firecrawl API:

```json
{
    "mcpServers": {
        "github.com/mendableai/firecrawl-mcp-server": {
            "autoApprove": [],
            "disabled": false,
            "timeout": 60,
            "type": "stdio",
            "command": "npx",
            "args": ["-y", "firecrawl-mcp"],
            "env": {
                "FIRECRAWL_API_KEY": "fc-YOUR_API_KEY_HERE"
            }
        }
    }
}
```

**Important:** For cloud API, replace `fc-YOUR_API_KEY_HERE` with your actual Firecrawl API key.

## Step 6: Restart VS Code

After updating the configuration file, restart VS Code to load the new MCP server.

## Step 7: Verify Installation

Once VS Code restarts, you should see the Firecrawl MCP server listed in the "Connected MCP Servers" section of your Cline interface. The server should show as connected and provide access to various tools.

## Available Tools

The Firecrawl MCP server provides these tools:

### Core Tools

-   **firecrawl_scrape**: Scrape content from a single URL
-   **firecrawl_batch_scrape**: Scrape multiple URLs efficiently
-   **firecrawl_map**: Discover URLs on a website
-   **firecrawl_crawl**: Crawl entire websites with depth control
-   **firecrawl_search**: Search the web for information

### Advanced Tools

-   **firecrawl_extract**: Extract structured data using LLM
-   **firecrawl_deep_research**: Conduct comprehensive research
-   **firecrawl_generate_llmstxt**: Generate LLMs.txt files

### Status Tools

-   **firecrawl_check_crawl_status**: Check crawl job status
-   **firecrawl_check_batch_status**: Check batch operation status

## Usage Examples

Once installed, you can use commands like:

-   "Scrape the content from https://example.com"
-   "Search the web for information about AI developments in 2024"
-   "Extract product information from these e-commerce pages"
-   "Map all URLs on the company website"
-   "Conduct deep research on renewable energy trends"

## Configuration Options

### Environment Variables

You can customize the server behavior with additional environment variables:

```json
"env": {
  "FIRECRAWL_API_KEY": "fc-YOUR_API_KEY",
  "FIRECRAWL_RETRY_MAX_ATTEMPTS": "5",
  "FIRECRAWL_RETRY_INITIAL_DELAY": "2000",
  "FIRECRAWL_RETRY_MAX_DELAY": "30000",
  "FIRECRAWL_CREDIT_WARNING_THRESHOLD": "1000",
  "FIRECRAWL_CREDIT_CRITICAL_THRESHOLD": "100"
}
```

### Self-Hosted Firecrawl

If you're using a self-hosted Firecrawl instance, add:

```json
"env": {
  "FIRECRAWL_API_URL": "https://firecrawl.your-domain.com",
  "FIRECRAWL_API_KEY": "your-api-key"
}
```

## Troubleshooting

### Server Not Connecting

-   Verify your API key is correct and starts with "fc-"
-   Check that Node.js is properly installed
-   Ensure the npm global installation completed successfully
-   Restart VS Code after configuration changes

### Rate Limiting

-   The server includes automatic retry logic with exponential backoff
-   Monitor your API credit usage through the Firecrawl dashboard
-   Adjust retry settings if needed using environment variables

### Large Responses

-   Some operations (like crawling) can return large amounts of data
-   Use appropriate limits and filters to manage response sizes
-   Consider using batch operations for multiple URLs

## API Limits and Pricing

-   Free tier: Limited requests per month
-   Paid plans: Higher limits and additional features
-   Check [Firecrawl pricing](https://www.firecrawl.dev/pricing) for current limits

## Security Notes

-   Keep your API key secure and never commit it to version control
-   The API key is stored in the MCP settings file on your local machine
-   Consider using environment variables for additional security

## Support

-   [Firecrawl Documentation](https://docs.firecrawl.dev/)
-   [Firecrawl MCP Server GitHub](https://github.com/mendableai/firecrawl-mcp-server)
-   [Cline Documentation](https://docs.cline.bot/)

## Next Steps

After installation, try asking Cline to:

1. Scrape a simple webpage to test basic functionality
2. Search for current information on a topic of interest
3. Extract structured data from a product page
4. Map the structure of a website you're interested in

The Firecrawl MCP server significantly enhances Cline's ability to gather and process web-based information for your projects.
