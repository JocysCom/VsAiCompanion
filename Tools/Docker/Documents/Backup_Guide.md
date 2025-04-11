# Docker/Podman Backup & Restore Guide (Windows) for n8n

This guide provides a simple and clear process to back up and restore your n8n service on Windows. It covers two separate backups:
- **Docker Image** – Contains the complete application (OS layers, application code, configuration, and metadata).
- **Docker Volume** – Contains persistent user data (databases, uploads, logs, etc.).

Environment variables and external configuration files (e.g., docker‑compose, .env) must be backed up separately.

## Naming Convention

Backup files follow this pattern:
```
{name}-{type}-{YYYYMMDD-HHMM}.tar
```
For example, with:
- **Container Name:** `n8n`
- **Volume Name:** `n8n_data`

You might have:
- **Volume Backup File:** `n8n-volume-20250101-1450.tar`
- **Image Backup File:** `n8n_data-image-20250101-1455.tar`

## Set Global Variables (PowerShell)

```powershell
$global:containerName = "n8n"
$global:volumeName = "n8n_data"
```

## 1. Docker Image (Application) Backup & Restore

### What’s Stored
- The entire application image including OS layers, code, and metadata.

### Backup Command

```powershell
# Generate a timestamp (e.g., 20250101-1455)
$timestamp = (Get-Date -Format "yyyyMMdd-HHmm")
$backupFile = "$global:volumeName-image-$timestamp.tar"
docker save -o $backupFile docker.io/n8nio/n8n:latest
```

### Restore Command

```powershell
# Adjust the filename as needed if it differs from the example below.
docker load -i n8n_data-image-20250101-1455.tar
```

## 2. Docker Volume (User Data) Backup & Restore

### What’s Stored
- All persistent user data stored in the volume.

### Backup Command

```powershell
# Generate a timestamp (e.g., 20250101-1450)
$timestamp = (Get-Date -Format "yyyyMMdd-HHmm")
$backupFile = "$global:containerName-volume-$timestamp.tar"
docker run --rm -v $global:containerName:/volume -v ${PWD}:/backup alpine sh -c "tar czf /backup/$backupFile -C /volume ."
```

### Restore Command

```powershell
# Adjust the filename as necessary.
docker run --rm -v $global:containerName:/volume -v ${PWD}:/backup alpine sh -c "cd /volume && tar xzf /backup/n8n-volume-20250101-1450.tar"
```

## Additional Notes

- **Environment Variables & Configurations:**  
  Ensure that you back up your docker‑compose files, .env files, and any other configuration files separately.

- **Timestamp Usage:**  
  Including the timestamp in the backup filename prevents accidental overwrites and helps manage backup versions.

This concise process ensures you can fully restore your n8n service by independently recovering the application image and the volume containing user data.