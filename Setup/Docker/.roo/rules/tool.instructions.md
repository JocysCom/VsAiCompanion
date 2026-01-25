## Tools

## Environment

Terminal sessions use PowerShell by default; always invoke scripts directly from the repository root.

### Forbidden PowerShell Host Prefixes

Do not prefix any command or script execution with `pwsh`, `powershell`, or `powershell.exe`, and do not use host-wrapper flags such as `-Command`, `-File`, `-NoProfile`, or `-ExecutionPolicy`. Commands must be provided as native PowerShell statements, and scripts must be invoked directly.

Examples:

```powershell
# WRONG (host prefix + host wrapper flags)
pwsh -NoProfile -ExecutionPolicy Bypass -File .\.ai\Scripts\Start-Local.ps1 MONITOR
powershell -Command "Get-ChildItem"

# VALID (direct invocation)
.\.ai\Scripts\Start-Local.ps1 MONITOR

# VALID (native PowerShell statement)
Get-ChildItem -Path . -Recurse -Filter *.csproj -File
```
