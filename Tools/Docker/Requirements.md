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
ML - Menu Loop (Uses the generic Invoke-MenuLoop function).
RM - Remove Container (Uses the generic Remove-ContainerAndVolume function).

| Script                               | HE | SR | CS | IM | ME | EL | SL | DF | CG | GP | PP | SE | AP | TC | HT | WB | BC | BS | RC | UP | RS | RV | CI | CW | ML | RM |
|--------------------------------------|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|
| Setup_0_*.ps1                        | HE |    |    |    |    | EL | SL | DF | CG | GP | PP | SE | AP | TC | HT | WB | BC | BS | RC | UP | RS | RV | CI | CW | ML | RM |
| Setup_1_WSL2.ps1                     | HE | SR |    |    |    | EL | SL |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    | CW |    |    |
| Setup_1a_Docker.ps1                  | HE | SR | CS | IM |    | EL | SL | DF |    | GP |    |    | AP |    |    |    |    |    |    |    |    |    |    | CW |    |    |
| Setup_1a_Podman.ps1                  | HE | SR | CS | IM | ME |    | SL | DF |    |    | PP |    | AP |    |    |    |    |    |    |    | RS | RV |    | CW | ML | RM |
| Setup_1a_Podman_ExportContainers.ps1 | HE |    |    |    |    |    |    |    |    |    |    |    |    |    |    |    | BC |    |    |    |    |    |    |    |    |    |
| Setup_1a_Podman_Restore.ps1          | HE |    |    | IM | ME |    |    |    |    |    |    |    |    |    |    |    |    |    | RC |    |    |    |    |    | ML |    |
| Setup_1b_BackupRestore.ps1           | HE | SR |    |    | ME |    |    |    |    |    |    | SE |    |    |    |    | BC |    | RC |    |    |    |    |    | ML |    |
| Setup_1c_Portainer.ps1               | HE | SR | CS | IM | ME | EL | SL |    |    | GP | PP | SE |    | TC | HT |    |    | BS | RC | UP | RS |    |    |    | ML | RM |
| Setup_2a_Pipelines.ps1               | HE | SR | CS | IM | ME |    | SL | DF |    | GP | PP | SE |    | TC | HT |    |    | BS | RC | UP | RS |    |    |    | ML | RM |
| Setup_2b_OpenWebUI.ps1               | HE | SR | CS | IM | ME |    | SL |    |    | GP | PP | SE |    | TC | HT | WB |    | BS | RC | UP | RS |    | CI | CW | ML | RM |
| Setup_3_n8n.ps1                      | HE | SR | CS | IM | ME | EL | SL |    |    | GP | PP | SE |    | TC | HT |    |    | BS | RC | UP | RS |    |    |    | ML | RM |
| Setup_4_Firecrawl.ps1                | HE | SR | CS | IM | ME | EL | SL |    |    | GP |    |    |    | TC | HT |    |    | BS | RC | UP | RS |    |    |    | ML | RM |
| Setup_5_Qdrant.ps1                   | HE | SR | CS | IM | ME | EL | SL |    |    | GP | PP | SE |    | TC | HT |    |    | BS | RC | UP | RS |    |    |    | ML | RM |
| Setup_6_Embedding.ps1                | HE | SR | CS | IM | ME |    | SL |    |    |    |    |    |    | TC | HT |    |    |    |    | UP |    |    |    |    | ML | RM |
| Setup_6_Embedding_Test.ps1           | HE |    |    |    |    |    |    |    |    |    |    |    |    | TC |    |    |    |    |    |    |    |    |    |    |    |
| Setup_7_NocoDB.ps1                   | HE | SR | CS | IM | ME | EL | SL |    |    | GP | PP | SE |    | TC | HT |    |    | BS | RC | UP | RS |    |    |    | ML | RM |

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

If administrator privileges are required (e.g., for Docker Desktop, service management), the script must call the `Ensure-Elevated` function immediately after dot-sourcing `Setup_0_Core.ps1`:

```PowerShell
Ensure-Elevated
```

### SL - Set Script Location

Scripts should set their execution location to the script's directory using `Set-ScriptLocation` after ensuring elevation (if applicable):

```PowerShell
Set-ScriptLocation
```

### SE - Select Container Engine

Scripts supporting both Docker and Podman should prompt the user and set global variables for the engine name and path:

```PowerShell
$global:containerEngine = Select-ContainerEngine
if ($global:containerEngine -eq "docker") {
    # Ensure-Elevated # Call if Docker requires elevation
    $global:enginePath = Get-DockerPath
}
else {
    $global:enginePath = Get-PodmanPath
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
*   Backup: `Backup-ContainerState -Engine $global:enginePath -ContainerName $global:containerName`
*   Restore: `Restore-ContainerState -Engine $global:enginePath -ContainerName $global:containerName` (potentially with `-RestoreVolumes`)
*   Update: Use the `Update-Container` function, providing a script block (`$RunFunction`) that correctly runs the specific container with its required options. `Check-AndRestoreBackup` (which calls `Restore-ContainerImage`) is often used within the install function called by `Update-Container`.

### ME - Menu Display Function

Scripts with a menu must define a `Show-ContainerMenu` function that displays the available options clearly. The standard menu should include Install, Uninstall, Backup, Restore, Update System, Update User Data (if applicable), and Exit.

```PowerShell
function Show-ContainerMenu {
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

Scripts with a menu must use the generic `Invoke-MenuLoop` function from `Setup_0_Core.ps1` to handle the menu logic. Define a hashtable mapping menu choices (strings) to the corresponding action script blocks.

```PowerShell
$menuActions = @{
    "1" = { Install-SpecificContainer }
    "2" = { Uninstall-SpecificContainer }
    "3" = { Backup-SpecificContainer }
    "4" = { Restore-SpecificContainer }
    "5" = { Update-SpecificContainer }
    # "6" = { Update-SpecificUserData } # Optional
    # "A" = { Custom-Action } # Optional
}

Invoke-MenuLoop -ShowMenuScriptBlock ${function:Show-ContainerMenu} -ActionMap $menuActions -ExitChoice "0"
```

### RM - Remove Container

Scripts should use the generic `Remove-ContainerAndVolume` function from `Setup_0_ContainerMgmt.ps1` for the uninstall action. Pass the correct engine path, container name, and volume name.

```PowerShell
function Uninstall-SpecificContainer {
    Remove-ContainerAndVolume -Engine $global:enginePath -ContainerName "specific-container" -VolumeName "specific-volume"
}
