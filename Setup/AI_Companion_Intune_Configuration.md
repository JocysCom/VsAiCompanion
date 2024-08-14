# Microsoft Intune Configuration for JocysCom.VS.AiCompanion

## Setup

Follow these steps to configure Microsoft Intune for deploying the `JocysCom.VS.AiCompanion.Setup_x.y.x.msi` file:

### Step 1: Add the Application

1. **Sign into the Microsoft Endpoint Manager admin center:**
   - Go to [endpoint.microsoft.com](https://endpoint.microsoft.com/).

2. **Navigate to Apps:**
   - Select **Apps** > **All apps** > **Add**.

3. **Select App Type:**
   - Choose **Line-of-business app**.

4. **Upload the MSI File:**
   - Under **App information**, upload `JocysCom.VS.AiCompanion.Setup_x.y.x.msi`.

### Step 2: Configure Detection Rules

1. **Add Detection Rule:**
   - In the **Detection rules** page, select **Add** a manually configured detection rule.

2. **Set Rule Type to Script:**
   - Configure the **Rule type** to **Script**.

3. **Upload the Detection Script:**
   - Upload the `AI_Companion_VersionCheck.ps1` script.
   - Ensure the script runs with the appropriate context (user or system).

### Step 3: Configure App Information

1. **Enter App Details:**
   - **Name**: Enter "VS AI Companion".
   - **Platform**: Choose Windows 10 and later.
   - **Description**: "Installs the VS AI Companion."

2. **Assign the App:**
   - Under **Assignments**, select the target users/groups.

## Update

To update the installation of JocysCom.VS.AiCompanion to a new MSI version:

1. **Repeat App Addition Process:**
   - Upload the new version of the MSI to Intune by repeating the steps in **Setup Step 1**.

2. **Update Detection Script:**
   - Open the `AI_Companion_VersionCheck.ps1` script.
   - Update the line `$currentVersionMsi = [Version]"x.y.z"` to reflect the new MSI version number.

3. **Ensure Detection Rules are Up-to-Date:**
   - Confirm that the latest `AI_Companion_VersionCheck.ps1` script reflects these changes for version checking.

4. **Reassign the App:**
   - Reassign the updated application to target users/groups.

For a detailed guide on app deployment using Microsoft Intune, refer to the official [Microsoft Documentation](https://learn.microsoft.com/en-us/mem/intune/apps/apps-deploy).