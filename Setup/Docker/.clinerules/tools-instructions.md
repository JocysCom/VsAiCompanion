## Tools

## Environment

Terminal sessions use PowerShell by default; always invoke scripts directly from the repository root. Do not prefix script execution with `pwsh`, `powershell`, or `powershell.exe`. Example:

WRONG: powershell -NoProfile -ExecutionPolicy Bypass -File .\.ai\Scripts\Start-Local.ps1 MONITOR
VALID: .\.ai\Scripts\Start-Local.ps1 MONITOR

