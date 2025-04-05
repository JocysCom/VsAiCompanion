# PowerShell Script Requirements Analysis

**Purpose:** This document analyzes a collection of PowerShell setup scripts (`.ps1`) to identify common features, patterns, and potential inconsistencies. It serves as a basis for standardizing the scripts and ensuring consistent functionality.

**Goals:**

*   Identify and define common features across the PowerShell scripts using two-letter codes.
*   Create a table summarizing which features are present in each script.
*   Define specific requirements for key features (e.g., shared library reference, menu structure).
*   Provide a reference for future script maintenance, standardization, and identification of missing features or inconsistencies.

**System Prompt to Recreate:**

```text
Analyze all `.ps1` files in the current directory. Identify common features (like headers, shared library references, container installation, menu presence, specific function calls from Setup_0.ps1, etc.). Assign a unique two-letter code to each feature. Create a markdown file named `Requirements.md`. This file should contain:
1. A "Features" section listing each two-letter code and its description.
2. A table with script names in the first column and feature codes as subsequent column headers. Use the feature codes to mark the presence of a feature in each script row.
3. A "Feature requirements" section detailing specific implementation requirements for key features (e.g., 'SR - Shared Library Reference', 'ME - Menu'). Include code examples where appropriate.
Ensure the table uses the feature codes directly in the cells, not checkboxes or other symbols.
```

---

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
RV - Refresh Environment Variables (Refreshes the current session's environment variables).
RS - Restore Container State (restores a container from a previously saved backup tar file).
CI - Check Image Update Available (Checks if a newer version of a container image is available from its registry).
CW - Check WSL Status (Verifies WSL installation and required service status).

| Script                               | HE | SR | CS | IM | ME | EL | SL | DF | CG | GP | PP | SE | AP | TC | HT | WB | BC | BS | RC | UP | RS | RV | CI | CW |
|--------------------------------------|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|
| Setup_0.ps1                          | HE | SR |    |    |    | EL | SL | DF | CG | GP | PP | SE | AP | TC | HT | WB | BC | BS | RC | UP | RS | RV | CI | CW |
| Setup_1_WSL2.ps1                     | HE | SR |    |    |    | EL | SL |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    | CW |
| Setup_1a_Docker.ps1                  | HE | SR | CS | IM |    | EL | SL | DF |    | GP |    |    | AP |    |    |    |    |    |    |    |    |    |    | CW |
| Setup_1a_Podman.ps1                  | HE | SR | CS | IM | ME |    | SL | DF |    |    | PP |    | AP |    |    |    |    |    |    |    | RS | RV |    | CW |
| Setup_1a_Podman_ExportContainers.ps1 | HE |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    | BC |    |    |    |    |    |    |    |
| Setup_1a_Podman_Restore.ps1          | HE |    |    | IM | ME |    |    |    |    |    |    |    |    |    |    |    |    |    | RC |    |    |    |    |    |
| Setup_1b_BackupRestore.ps1           | HE | SR |    |    | ME |    |    |    |    |    |    | SE |    |    |    |    | BC |    | RC |    |    |    |    |    |
| Setup_1c_Portainer.ps1               | HE | SR | CS | IM | ME | EL | SL |    |    | GP | PP | SE |    | TC | HT |    |    | BS | RC | UP | RS |    |    |    |
| Setup_2a_Pipelines.ps1               | HE | SR | CS | IM | ME |    | SL | DF |    | GP | PP | SE |    | TC | HT |    |    | BS | RC | UP | RS |    |    |    |
| Setup_2b_OpenWebUI.ps1               | HE | SR | CS | IM | ME |    | SL |    |    | GP | PP | SE |    | TC | HT | WB |    | BS | RC | UP | RS |    | CI | CW |
| Setup_3_n8n.ps1                      | HE | SR | CS | IM | ME | EL | SL |    |    | GP | PP | SE |    | TC | HT |    |    | BS | RC | UP | RS |    |    |    |
| Setup_4_Firecrawl.ps1                | HE | SR | CS | IM | ME | EL | SL |    |    | GP |    |    |    | TC | HT |    |    | BS | RC | UP | RS |    |    |    |
| Setup_5_Qdrant.ps1                   | HE | SR | CS | IM | ME | EL | SL |    |    | GP | PP | SE |    | TC | HT |    |    | BS | RC | UP | RS |    |    |    |
| Setup_6_Embedding.ps1                | HE | SR | CS | IM | ME |    | SL |    |    |    |    |    |    | TC | HT |    |    |    |    | UP |    |    |    |    |
| Setup_6_Embedding_Test.ps1           | HE |    |    |    |    |    |    |    |    |    |    |    |    | TC |    |    |    |    |    |    |    |    |    |    |
| Setup_7_NocoDB.ps1                   | HE | SR | CS | IM | ME | EL | SL |    |    | GP | PP | SE |    | TC | HT |    |    | BS | RC | UP | RS |    |    |    |

## Feature requirements

### SR - References Setup_0.ps1 with shared functions.

Script must include shared library:

```PowerShell
# Dot-source the common functions file.
. "$PSScriptRoot\Setup_0.ps1"
```

### ME - Have the container menu.

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
