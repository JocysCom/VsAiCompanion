# How to Install Tavily MCP Server for Cline on Windows

This guide details the steps to install and configure the Tavily MCP server (`github.com/tavily-ai/tavily-mcp`) for use with Cline on a Windows system, incorporating common troubleshooting steps.

## Prerequisites

Before starting, ensure you have the following:

1.  **Node.js:** Version 20 or higher is required. Verify your version by opening Command Prompt or PowerShell and running `node --version`. If needed, download and install the latest LTS version from [https://nodejs.org/](https://nodejs.org/). Remember to restart VS Code/Cline after installation.
2.  **Git:** Required for the recommended installation method. Download and install from [https://git-scm.com/downloads](https://git-scm.com/downloads).
3.  **Tavily API Key:** Obtain a free API key from [https://app.tavily.com/home](https://app.tavily.com/home).
4.  **Cline:** Installed and running in VS Code.

## Installation Methods

There are two primary methods mentioned in the official documentation. Based on potential environment issues on Windows, the Git method is often more reliable.

### Method 1: NPX (Potential Issues)

The official README suggests using `npx`:

```bash
npx -y tavily-mcp@0.1.4
```

And configuring Cline settings like this:

```json
// cline_mcp_settings.json (Example - May encounter issues)
{
	"mcpServers": {
		"github.com/tavily-ai/tavily-mcp": {
			"command": "npx",
			"args": ["-y", "tavily-mcp@0.1.4"],
			"env": {
				"TAVILY_API_KEY": "YOUR_TAVILY_API_KEY_HERE"
			},
			"disabled": false,
			"autoApprove": []
		}
		// ... other servers
	}
}
```

**Potential Issue:** You might encounter a `spawn npx ENOENT` error. This typically means the environment where Cline launches the server cannot find the `npx` executable, even if Node.js is installed correctly. This can be due to PATH variable issues specific to the VS Code/Cline execution context. If you see this error, proceed with the Git method.

### Method 2: Git Installation (Recommended)

This method involves cloning the repository locally and running the server directly with Node.js, bypassing potential `npx` path issues.

1.  **Create a Directory (Optional but Recommended):**
    Organize your MCP servers. Choose a location, for example:
    `C:\Users\YOUR_USERNAME\Documents\Cline\MCP`
    Create a subdirectory for Tavily:

    ```bash
    mkdir "C:\Users\YOUR_USERNAME\Documents\Cline\MCP\tavily-mcp"
    ```

    _(Replace `YOUR_USERNAME` with your actual Windows username)_

2.  **Clone the Repository:**
    Open Command Prompt or PowerShell and run:

    ```bash
    git clone https://github.com/tavily-ai/tavily-mcp.git "C:\Users\YOUR_USERNAME\Documents\Cline\MCP\tavily-mcp"
    ```

3.  **Install Dependencies:**
    Navigate into the cloned directory and install packages:

    ```bash
    cd "C:\Users\YOUR_USERNAME\Documents\Cline\MCP\tavily-mcp"
    npm install
    ```

4.  **Build the Project:**
    Run the build script:
    ```bash
    npm run build
    ```
    This compiles the necessary JavaScript files into the `build` directory.

## Cline Configuration (Git Method)

1.  **Locate Cline Settings:** Find the `cline_mcp_settings.json` file. On Windows, it's typically located at:
    `%APPDATA%\Code\User\globalStorage\saoudrizwan.claude-dev\settings\cline_mcp_settings.json`
    You can open it directly in VS Code.

2.  **Edit the Settings:** Add or modify the entry for the Tavily server. Ensure you use the correct path to the `index.js` file created during the build step. Replace `YOUR_USERNAME` and `YOUR_TAVILY_API_KEY_HERE`.

    ```json
    {
    	"mcpServers": {
    		// ... other servers like qdrant might be here ...
    		"github.com/tavily-ai/tavily-mcp": {
    			"command": "node",
    			"args": [
    				// Use double backslashes for paths in JSON on Windows
    				"C:\\Users\\YOUR_USERNAME\\Documents\\Cline\\MCP\\tavily-mcp\\build\\index.js"
    			],
    			"env": {
    				"TAVILY_API_KEY": "YOUR_TAVILY_API_KEY_HERE"
    				// Add the line below ONLY if you encounter SSL errors (see Troubleshooting)
    				// "NODE_TLS_REJECT_UNAUTHORIZED": "0"
    			},
    			"disabled": false,
    			"autoApprove": []
    		}
    	}
    }
    ```

3.  **Save and Restart:** Save the `cline_mcp_settings.json` file. It's recommended to restart VS Code to ensure Cline picks up the changes and launches the server correctly.

## Troubleshooting

-   **Node.js Version:** Double-check you are running Node.js v20 or higher (`node --version`). Update if necessary.
-   **`spawn npx ENOENT`:** If using the NPX method, this indicates `npx` wasn't found. Switch to the **Git Installation Method**.
-   **`Error: self-signed certificate in certificate chain`:** This SSL/TLS error can occur if your network environment (e.g., corporate proxy, firewall) interferes with certificate validation when the server tries to contact the Tavily API.
    -   **Workaround:** Add the following line to the `env` section for the Tavily server in `cline_mcp_settings.json`:
        ```json
        "NODE_TLS_REJECT_UNAUTHORIZED": "0"
        ```
    -   **Security Note:** This disables TLS certificate validation for the Node.js process running the Tavily server. While necessary in some environments, be aware that this reduces security as it won't verify the authenticity of the Tavily API server's certificate. Use this workaround only if required and understand the implications.
    -   Remember to save the settings file and potentially restart VS Code after adding this environment variable.

## Verification

After configuration and restarting Cline, you can ask Cline to use one of the Tavily tools to verify the connection:

"Use tavily-search to search the web for 'latest AI developments'."

If the search runs successfully, the installation is complete.
