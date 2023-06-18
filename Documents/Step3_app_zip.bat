@ECHO off
SET wra="%ProgramFiles%\WinRAR\winrar.exe"
IF NOT EXIST %wra% SET wra="%ProgramFiles(x86)%\WinRAR\winrar.exe"
IF NOT EXIST %wra% SET wra="%ProgramW6432%\WinRAR\winrar.exe"
IF NOT EXIST %wra% SET wra="D:\Program Files\WinRAR\winrar.exe"
SET zip=%wra% a -ep
:: ---------------------------------------------------------------------------
IF NOT EXIST Files\nul MKDIR Files
::-------------------------------------------------------------
:: Archive MSIL Application
CALL:CRE ..\E2EETool\bin\Release\netcoreapp3.1\publish\win-x64  JocysCom.Tools.E2EETool
ECHO.
pause
GOTO:EOF

::-------------------------------------------------------------
:CRE :: Sign and Timestamp Code
::-------------------------------------------------------------
SET src=%~1
SET arc=Files\%~2.zip
ECHO.
IF NOT EXIST "%src%\JocysCom.Tools.E2EETool.exe" (
  ECHO "%src%\JocysCom.Tools.E2EETool.exe" not exist. Skipping.
  GOTO:EOF
)
ECHO Creating: %arc%
:: Create Archive.
IF EXIST %arc% DEL %arc%
%zip% %arc% %src%\%~2.exe
GOTO:EOF