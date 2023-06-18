@ECHO OFF
COPY /Y "..\bin\Release\JocysCom.VS.AiCompanion.vsix" "JocysCom.VS.AiCompanion.vsix"
CALL:SIG "JocysCom.VS.AiCompanion.vsix"
echo.
pause

GOTO:EOF
::=============================================================
:SIG :: Sign and Timestamp Code
::-------------------------------------------------------------
:: SIGNTOOL.EXE Note:
:: Use the Windows 7 Platform SDK web installer that lets you
:: download just the components you need - so just choose the
:: ".NET developer \ Tools" and deselect everything else.
echo.
IF NOT EXIST "%~1" (
  ECHO "%~1" not exist. Skipping.
  GOTO:EOF
)
SET sgt=Tools\VSIXSignTool.exe
echo %sgt%
echo.
:: Other options.
set pfx=D:\_Backup\Configuration\SSL\Code Sign - Evaldas Jocys\2020\Evaldas_Jocys.pfx
set vsg=http://timestamp.comodoca.com
if not exist "%sgt%" CALL:Error "%sgt%"
if not exist "%~1"   CALL:Error "%~1"
if not exist "%pfx%" CALL:Error "%pfx%"
"%sgt%" sign /f "%pfx%" /sha1 cc747560b7f9d3641f2211a03c698e820f06efd9 /fd sha256 /td sha256 /tr "%vsg%" /v "%~1"
GOTO:EOF

:Error
echo File doesn't Exist: "%~1"
pause
