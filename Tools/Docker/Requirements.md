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

HE - Have a standard header block (see requirements below).
SR - References necessary Setup_0_*.ps1 files with shared functions.
CS - Installs container server (Docker/Podman CLI/Engine or builds from source).
IM - Installs container images (pulls pre-built images or builds from source).
ME - Has a main container management menu loop.
EL - Ensure Elevated (checks for administrator privileges using Test-AdminPrivilege).
SL - Set Script Location (sets the script's working directory using Set-ScriptLocation).
DF - Download File (downloads a file from a URL using Invoke-DownloadFile).
CG - Check Git (checks if Git is installed using Test-GitInstallation).
GP - Get Docker Path (gets the path to the Docker executable, usually via Get-EnginePath).
PP - Get Podman Path (gets the path to the Podman executable, usually via Get-EnginePath).
SE - Select Container Engine (prompts the user to choose Docker or Podman using Select-ContainerEngine).
AP - Test Application Installed (checks if an application is installed using Test-ApplicationInstalled).
TC - Test TCP Port (tests if a TCP port is open using Test-TCPPort).
HT - Test HTTP Port (tests if an HTTP port is open using Test-HTTPPort).
WB - Test WebSocket Port (tests if a WebSocket port is open using Test-WebSocketPort).
BC - Backup Container Image (backs up a container image to a tar file using Backup-ContainerImage).
BS - Backup Container Volume (backs up a container's associated volume using Backup-ContainerVolume - tar method).
RC - Restore Container Image (restores a container image from a tar file using Restore-ContainerImage or Test-AndRestoreBackup).
UP - Update Container (updates a container image using Update-Container or custom build/run logic).
RV - Refresh Environment Variables (Refreshes the current session's environment variables using Update-EnvironmentVariable).
RS - Restore Container Volume (restores a container's associated volume from a tar backup using Restore-ContainerVolume).
CI - Check Image Update Available (Checks if a newer version of a container image is available using Test-ImageUpdateAvailable).
CW - Check WSL Status (Verifies WSL installation and required service status using Test-WSLStatus).
ML - Menu Loop (Uses the generic Invoke-MenuLoop function for main menu or internal choices).
RM - Remove Container (Uses the generic Remove-ContainerAndVolume function, may include related resources like networks/other containers).
ST - Show/Test Status (Displays container info, status, and tests connectivity using Show-ContainerStatus).

| Script                               | HE | SR | CS | IM | ME | EL | SL | DF | CG | GP | PP | SE | AP | TC | HT | WB | BC | BS | RC | UP | RS | RV | CI | CW | ML | RM | ST |
|--------------------------------------|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|
| Setup_0_\*.ps1                       | HE |    |    |    |    | EL | SL | DF | CG | GP | PP | SE | AP | TC | HT | WB | BC | BS | RC | UP | RS | RV | CI | CW | ML | RM | ST |
| Setup_1_WSL2.ps1                     | HE | SR |    |    |    | EL | SL |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    | CW | ML |    |    |
| Setup_1a_Docker.ps1                  | HE | SR | CS | IM |    | EL | SL | DF |    | GP |    |    | AP |    |    |    |    |    |    |    |    |    |    | CW | ML |    |    |
| Setup_1a_Podman.ps1                  | HE | SR | CS | IM | ME | EL | SL | DF |    |    | PP |    | AP |    |    |    |    |    |    |    |    | RV |    | CW | ML |    |    |
| Setup_1a_Podman_ExportContainers.ps1 | HE |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    | BC |    |    |    |    |    |    |    |    |    |    |
| Setup_1a_Podman_Restore.ps1          | HE |    |    | IM | ME |    |    |    |    |    |    |    |    |    |    |    |    |    | RC |    |    |    |    |    | ML |    |    |
| Setup_1b_BackupRestore.ps1           | HE | SR |    |    | ME |    | SL |    |    | GP | PP | SE |    |    |    |    | BC |    | RC |    |    |    |    |    | ML |    |    |
| Setup_1c_Portainer.ps1               | HE | SR | CS | IM | ME | EL | SL |    |    | GP | PP | SE |    | TC | HT |    |    | BS | RC | UP | RS |    |    |    | ML | RM | ST |
| Setup_2a_Pipelines.ps1               | HE | SR | CS | IM | ME |    | SL |    |    | GP | PP | SE |    | TC | HT |    |    | BS |    | UP | RS |    |    |    | ML | RM | ST |
| Setup_2b_OpenWebUI.ps1               | HE | SR | CS | IM | ME |    | SL |    |    | GP | PP | SE |    | TC | HT | WB |    | BS | RC | UP | RS |    | CI |    | ML | RM | ST |
| Setup_3_n8n.ps1                      | HE | SR | CS | IM | ME | EL | SL |    |    | GP | PP | SE |    | TC | HT |    |    | BS | RC | UP | RS |    |    |    | ML | RM | ST |
| Setup_3_n8n_Export.ps1               | HE | SR |    |    |    | EL | SL |    |    | GP | PP | SE |    |    |    |    |    | BS |    |    |    |    |    |    |    |    |
| Setup_4_Firecrawl.ps1                | HE | SR | CS | IM | ME | EL | SL |    |    | GP |    |    |    | TC | HT |    |    | BS | RC | UP | RS |    |    |    | ML | RM | ST |
| Setup_5_Qdrant.ps1                   | HE | SR | CS | IM | ME | EL | SL |    |    | GP | PP | SE |    | TC | HT |    |    | BS | RC | UP | RS |    |    |    | ML | RM | ST |
| Setup_5_Qdrant_MCP_Server.ps1        | HE | SR | CS | IM | ME | EL | SL |    | CG | GP | PP | SE |    | TC |    |    |    | BS |    | UP | RS |    |    |    | ML | RM | ST |
| Setup_6_Embedding.ps1                | HE | SR | CS | IM | ME |    | SL |    |    |    | PP |    |    | TC | HT |    |    |    |    | UP |    |    |    |    | ML | RM | ST |
| Setup_6_Embedding_Test.ps1           | HE |    |    |    |    |    |    |    |    |    |    |    |    | TC |    |    |    |    |    |    |    |    |    |    |    |    |
| Setup_7_NocoDB.ps1                   | HE | SR | CS | IM | ME | EL | SL |    |    | GP | PP | SE |    | TC | HT |    |    | BS | RC | UP | RS |    |    |    | ML | RM | ST |

## Feature requirements

### HE - Header

Script must start with a standard header block:

```PowerShell
################################################################################
# File         : [Your_Script_Name.ps1]
# Description  : [Brief description of the script's purpose]
# Usage        : [How to run the script, e.g., Run as Administrator]
################################################################################
```

### SR - Shared Library Reference

Scripts must include the specific shared library files (`Setup_0_*.ps1`) they need using dot-sourcing at the beginning, after the header and any `using namespace` statements. Import only the necessary files based on the functions used. **Do not import `Setup_0.ps1` directly.**

**Example:** A script using core functions, network tests, and container engine selection would import:
```PowerShell
# Dot-source the necessary helper function files.
. "$PSScriptRoot\Setup_0_Core.ps1"
. "$PSScriptRoot\Setup_0_Network.ps1"
. "$PSScriptRoot\Setup_0_ContainerEngine.ps1"
# Add other Setup_0_*.ps1 files as needed (e.g., Setup_0_BackupRestore.ps1)
```

### EL - Ensure Elevated

If administrator privileges are required (e.g., for Docker Desktop, service management), the script must call the `Test-AdminPrivilege` function immediately after dot-sourcing `Setup_0_Core.ps1`:

```PowerShell
Test-AdminPrivilege
```

### SL - Set Script Location

Scripts should set their execution location to the script's directory using `Set-ScriptLocation` after ensuring elevation (if applicable):

```PowerShell
Set-ScriptLocation
```

### SE - Select Container Engine

Scripts supporting both Docker and Podman should call the `Select-ContainerEngine` function. This function displays a menu, prompts the user, and returns the selected engine name ('docker' or 'podman'). It also sets script-scoped variables `$script:selectedEngine` and `$script:enginePath`. The calling script should typically assign the returned name to a global variable and can optionally retrieve the path from the script-scoped variable or call `Get-EnginePath` again.

```PowerShell
# Example in calling script:
$global:containerEngine = Select-ContainerEngine
if ($global:containerEngine -eq "docker") {
    # Test-AdminPrivilege # Call if Docker requires elevation
    $global:enginePath = Get-EnginePath -EngineName "docker" # Or use $script:enginePath if appropriate
}
elseif ($global:containerEngine -eq "podman") {
    $global:enginePath = Get-EnginePath -EngineName "podman" # Or use $script:enginePath if appropriate
}
else {
    Write-Error "No valid container engine selected. Exiting."
    exit 1
}
# Validate $global:enginePath is not null
```

### CS / IM - Container Installation Flow

The standard flow for installing a container service and its image should be:
1.  Check/Create necessary volumes (e.g., `[engine] volume create [volume_name]`).
2.  Check if the image exists locally (`[engine] images ...`).
3.  If the image doesn't exist, attempt to restore from backup (`Check-AndRestoreBackup`).
4.  If no backup restored, pull the image (`[engine] pull [image_name]`).
5.  Remove any existing container with the same name (`[engine] rm --force [container_name]`).
6.  Run the new container with appropriate options (`[engine] run --detach --name ... --publish ... --volume ... [image_name]`). Add comments explaining parameters.
7.  Wait for startup (`Start-Sleep`).
8.  Test connectivity (using TC, HT, WB).

### TC / HT / WB - Port Testing

After starting a container, connectivity should be tested using the relevant functions from `Setup_0_Network.ps1`:

```PowerShell
Test-TCPPort -ComputerName "localhost" -Port [port_number] -serviceName "[Service Name]"
Test-HTTPPort -Uri "http://localhost:[port_number]" -serviceName "[Service Name]"
# Test-WebSocketPort -Uri "ws://localhost:[port_number]/[path]" -serviceName "[Service Name]" # If applicable
```

### BS / RC / UP / RS - Backup, Restore, Update

Scripts providing Backup, Restore, and Update functionality via the menu should use the corresponding shared functions from `Setup_0_BackupRestore.ps1` and `Setup_0_ContainerMgmt.ps1`:
*   **BS - Backup Volume:** Use `Backup-ContainerVolume` to back up the container's associated data volume to a `.tar` file.
    ```PowerShell
    # Example Menu Action
    Backup-ContainerVolume -Engine $global:enginePath -EngineType $global:containerEngine -VolumeName $global:volumeName
    ```
*   **RC - Restore Image:** Use `Restore-ContainerImage` (or `Test-AndRestoreBackup`) to restore a container image from a `.tar` file.
    ```PowerShell
    # Example Menu Action (prompting for file)
    $backupFile = Read-Host "Enter path to image backup file (.tar)"
    Restore-ContainerImage -Engine $global:enginePath -BackupFile $backupFile
    ```
*   **UP - Update Container:** Use the `Update-Container` function. This function handles checking for image updates, removing the old container, and pulling the new image. The calling script's menu action must handle backup/restore prompts and starting the new container (often by providing a script block like `Invoke-StartSpecificContainerForUpdate` to the `-RunFunction` parameter, although this parameter is not currently implemented in `Update-Container`).
    ```PowerShell
    # Example Menu Action (simplified, assumes Update-Container handles start or caller does)
    Update-Container -Engine $global:enginePath -ContainerName $global:containerName -VolumeName $global:volumeName -ImageName $global:imageName
    ```
*   **RS - Restore Volume:** Use `Restore-ContainerVolume` to restore a container's data volume from a `.tar` backup file (prompts for selection).
    ```PowerShell
    # Example Menu Action
    Restore-ContainerVolume -Engine $global:enginePath -EngineType $global:containerEngine -VolumeName $global:volumeName
    ```

### ME - Menu Display Function

Scripts with a menu must define a `Show-ContainerMenu` function that displays the available options clearly. The standard menu should include Install, Uninstall, Backup, Restore, Update System, Update User Data (if applicable), and Exit. `Exit` and similar options must be assigned to `0`.

```PowerShell
function Show-ContainerMenu {
	[System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("PSAvoidUsingWriteHost", "", Justification="Write-Host is needed for the Read-Host prompt below.")]
	Write-Host "==========================================="
	Write-Host "[Specific Container Name] Menu"
	Write-Host "==========================================="
	Write-Host "1. Install container"
	Write-Host "2. Uninstall container"
	Write-Host "3. Backup Live container"
	Write-Host "4. Restore Live container"
	Write-Host "5. Update System"
	# Write-Host "6. Update User Data" # Optional
	# Write-Host "[Letter]. [Custom Action]" # Optional custom actions
	Write-Host "0. Exit menu"
}
```

### ML - Menu Loop

Scripts with a menu must use the generic `Invoke-MenuLoop` function from `Setup_0_Core.ps1` to handle the menu logic. Provide the menu title, an ordered hashtable of menu items (choice string -> description), and a hashtable mapping menu choices to action script blocks.

```PowerShell
# Define Menu Title and Items
$menuTitle = "[Specific Container Name] Menu"
$menuItems = [ordered]@{
	"1" = "Show Info & Test Connection"
	"2" = "Install container"
	"3" = "Uninstall container"
	"4" = "Backup Volume (User Data)"
	"5" = "Restore Volume (User Data)"
	"6" = "Update System (Image)"
	# ... other options ...
	"0" = "Exit menu"
}

# Define Menu Actions
$menuActions = @{
	"1" = { Show-ContainerStatus ... }
	"2" = { Install-SpecificContainer }
	"3" = { Remove-ContainerAndVolume ... }
	"4" = { Backup-ContainerVolume ... }
	"5" = { Restore-ContainerVolume ... }
	"6" = { Update-SpecificContainer }
	# ... other actions ...
    # "A" = { Custom-Action } # Optional
}

# Invoke the Menu Loop
Invoke-MenuLoop -MenuTitle $menuTitle -MenuItems $menuItems -ActionMap $menuActions -ExitChoice "0"
```

### RM - Remove Container

Scripts should use the generic `Remove-ContainerAndVolume` function from `Setup_0_ContainerMgmt.ps1` for the uninstall action. Pass the correct engine path, container name, and volume name.

```PowerShell
function Uninstall-SpecificContainer {
    Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName "specific-container" -VolumeName "specific-volume"
}

### ST - Show/Test Status

Scripts implementing this feature provide a menu option (typically '1') to display the container's current status and test its connectivity. This is achieved by calling the `Show-ContainerStatus` function from `Setup_0_ContainerMgmt.ps1`.

**Requirements:**
*   The script must dot-source `Setup_0_ContainerMgmt.ps1`.
*   If network tests (TCP, HTTP, WS) are included in the status check, `Setup_0_Network.ps1` must also be dot-sourced *before* `Setup_0_ContainerMgmt.ps1`.
*   The `Show-ContainerMenu` function must be updated to include the "Show Info & Test Connection" option, and subsequent options must be renumbered.
*   The `$menuActions` hashtable must be updated to map the corresponding menu choice (e.g., "1") to a script block that calls `Show-ContainerStatus`.
*   The call to `Show-ContainerStatus` should pass the necessary parameters, typically using the script's global variables for `$ContainerName`, `$ContainerEngine`, `$EnginePath`, and specific ports/paths/additional info relevant to the container being managed.

**Example (`$menuActions` entry):**
```PowerShell
$menuActions = @{
    "1" = {
        Show-ContainerStatus -ContainerName $global:containerName `
                             -ContainerEngine $global:containerEngine `
                             -EnginePath $global:enginePath `
                             -DisplayName "Specific Container" `
                             -TcpPort [TCP_PORT] `
                             -HttpPort [HTTP_PORT] `
                             # -WsPort [WS_PORT] ` # Optional
                             # -AdditionalInfo @{ "Key" = "Value" } # Optional
    }
    # ... other renumbered actions ...
}
```
