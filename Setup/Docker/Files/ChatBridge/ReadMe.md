# Browser Bridge for n8n — IIS `/proxy` Application

**Purpose:** Bridge WebSocket-based browser clients to n8n so an AI agent can call **client-side** functions (DOM, localStorage, UX tweaks) safely via a server-side tool.

**Example environment**

- Local debugging site (your app): `https://localhost:12345/`
- Proxy (bridge) app in IIS: `https://n8n.example.com/proxy`
- n8n server (reverse-proxied by IIS): `https://n8n.example.com`

**Data flow**

Browser (localhost) ──wss──>   https://<host>/proxy/ws
n8n Tools Agent     ──POST──>  https://<host>/proxy/api/chatbridge/invoke

Example embed (chat widget points at n8n):

Client side: <https://localhost:12345/>  ->  ChatWebhookUrl = "<https://n8n.example.com/webhook/><chat_id>/chat"
Server side bridge: <https://localhost:12345/>  <->  <https://n8n.example.com/proxy>  <->  <https://n8n.example.com/>

## Prerequisites (on the IIS server)

1) **ASP.NET Core Module** (ANCM) installed  

   ```powershell
   Import-Module WebAdministration; Get-WebGlobalModule | Where-Object {$_.Name -like 'AspNetCore*'}
   ```

   If not present, install the **.NET Hosting Bundle**:
   <https://dotnet.microsoft.com/en-us/download/dotnet/8.0>

2) **WebSocket Protocol** Windows feature enabled (Server Manager → Add Roles and Features).

## Step 2 — Publish minimal ChatBridge app

```powershell
dotnet publish ChatBridge.csproj -f net8.0 -c Release -o .\bin\publish
```

## Step 3 — Add IIS **Application** at `/proxy`

In **IIS Manager**: right-click your existing site (e.g., `n8n.example.com`) → **Add Application…**

- **Alias:** `proxy`  
- **Physical path:** the `publish` folder from Step 2  
- **App Pool:** create and set dedicated `proxy-app` pool with **.NET CLR = No Managed Code** (ANCM v2 hosts Kestrel)

## Step 4 — Wire up n8n + Browser

**Browser (your localhost app):**

- Embed the chat widget and pass a `sessionId` via `metadata`.
- Open a WebSocket to the bridge:

  ```
  wss://n8n.example.com/proxy/ws?sessionId=<uuid>
  ```

  (The browser automatically sends `Origin: https://localhost:12345`.)

**n8n Tools Agent:**

- Add **HTTP Request Tool** named `browser`:
  - **POST** `https://n8n.example.com/proxy/api/chatbridge/invoke`
  - **Body (Using Fields Below):**

	Name: sessionId
	Value (Expression): {{ $json.sessionId }}
	
	Name: tool
	Value AI Auto
	
	Name: tool
	Value AI Auto

- In the **Chat Trigger**, add `https://localhost:12345` to **Allowed Origins (CORS)**.
