## Features

HE - Have a header with file, name description and usage
SR - References Setup_0.ps1 with shared functions.
CS - Installs container server
IM - Installs container images
ME - Have the container menu.
EL - Ensure Elevated (checks for administrator privileges).
SL - Set Script Location (sets the script's working directory).
DF - Download File (downloads a file from a URL).
CG - Check Git (checks if Git is installed).
GP - Get Docker Path (gets the path to the Docker executable).
PP - Get Podman Path (gets the path to the Podman executable).
SE - Select Container Engine (prompts the user to choose Docker or Podman).
AP - Test Application Installed (checks if an application is installed).
TC - Test TCP Port (tests if a TCP port is open).
HT - Test HTTP Port (tests if an HTTP port is open).
WB - Test WebSocket Port (tests if a WebSocket port is open).
BC - Backup Container Image (backs up a container image to a tar file).
BS - Backup Container State (backs up a live running container by committing its state to an image and saving that image as a tar file).
RC - Restore Container Image (restores a container image from a tar file).
UP - Update Container (updates a container while preserving its configuration).
RE - Refresh Environment Variables (Refreshes the current session's environment variables).
RE - Restore Container State (restores a container from a previously saved backup tar file).
CI - Check Image Update Available (Checks if a newer version of a container image is available from its registry).
CW - Check WSL Status (Verifies WSL installation and required service status).

| Script                               | HE | SR | CS | IM | ME | EL | SL | DF | CG | GP | PP | SE | AP | TC | HT | WB | BC | BS | RC | UP | RE | RV | CI | CW |
|--------------------------------------|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|
| Setup_0.ps1                          | HE | SR |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |
| Setup_1_WSL2.ps1                     | HE | SR |    |    |    | EL | SL |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    | CW |
| Setup_1a_Docker.ps1                  | HE | SR | CS |    |    | EL | SL | DF |    | GP |    |    |    |    |    |    |    |    |    |    |    |    |    | CW |
| Setup_1a_Podman.ps1                  | HE | SR | CS | IM | ME |    | SL | DF |    |    | PP |    |    |    |    |    |    |    |    |    |    |    |    |    |
| Setup_1a_Podman_ExportContainers.ps1 | HE |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |
| Setup_1a_Podman_Restore.ps1          | HE |    |    | IM | ME |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |
| Setup_1b_BackupRestore.ps1           | HE | SR |    |    |    |    |    |    |    |    |    |    |    |    |    |    | BC |    | RC |    |    |    |    |    |
| Setup_1c_Portainer.ps1               | HE | SR |    |    | ME | EL | SL |    |    | GP |    | SE |    |    |    |    |    |    |    |    |    |    |    |    |
| Setup_2a_Pipelines.ps1               | HE | SR |    |    | ME |    | SL | DF |    |    |    | SE |    |    |    |    |    |    |    |    |    |    |    |    |
| Setup_2b_OpenWebUI.ps1               | HE | SR |    |    | ME |    | SL |    |    |    |    |    |    |    |    | WB |    |    |    |    |    |    | CW |    |
| Setup_3_n8n.ps1                      | HE | SR |    |    | ME |    | SL |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |
| Setup_4_Firecrawl.ps1                | HE | SR |    |    | ME |    | SL |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |
| Setup_5_Qdrant.ps1                   | HE | SR |    |    | ME |    | SL |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |
| Setup_6_Embedding.ps1                | HE | SR |    |    |    |    | SL |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |
| Setup_6_Embedding_Test.ps1           | HE |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |
| Setup_7_NocoDB.ps1                   | HE | SR |    |    | ME |    | SL |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |

## Feature requirements:

### HE - Shared Library Reference

Script must include shared library:

```PowerShell
# Dot-source the common functions file.
. "$PSScriptRoot\Setup_0.ps1"
```

### ME - Menu

Script must have at least 7 menu choices:

```text
===========================================
Container Menu - {Container_Name}
===========================================
1. Install container
2. Uninstall container
3. Backup Live container
4. Restore Live container
5. Update System
6. Update User Data
0. Exit menu
