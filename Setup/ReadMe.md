# VS AI Companion Installer - Update Requirements

## Installation Path and Settings
Setup installs both the application and its settings for the current user only and does not affect other user accounts on the same machine.
The installation will appear under the Add/Remove Programs list for the current user only.
The application and its settings are installed in the following directory:

`%LOCALAPPDATA%\<company>\<product>\`

For example:

`C:\Users\<UserName>\AppData\Local\Jocys.com\VS AI Companion\`

## Overview
To ensure that the Visual Studio Installer properly handles upgrades by uninstalling the previous version before installing the new one, certain properties in the installer project must be correctly configured. This guide outlines these requirements.

## Requirements

### 1. Product Version
- **Must Change**: Increment the `ProductVersion` for each new release.
  - Format: `major.minor.build`
  - Example: Change from `1.12.25` to `1.12.26` or `1.13.0`

### 2. Package Code
- **Must Change**: Generate a new and unique `PackageCode` for each build.
  - Visual Studio typically handles this automatically with each rebuild.
  - Ensure it changes between builds to identify new packages distinctly.

### 3. Product Code
- **Must Change**: Each version of the install package should have a unique `ProductCode`.
  - Ensure it is different for each new release.
  - Visual Studio generally updates this automatically during the build process.

### 4. Upgrade Code
- **Must Remain Constant**: The `UpgradeCode` should stay the same across different versions of the product.
  - This identifier allows the installer to recognize and manage the upgrade process.
  - It should not change between different releases of your application.

### 5. Remove Previous Versions
- **Set to True**: The `RemovePreviousVersions` property should be set to `True` to ensure the installer removes the older version before installing the new one.
