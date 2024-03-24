@ECHO off
SET msv=%ProgramFiles%\Microsoft Visual Studio
SET exe=%msv%\2022\Professional\Common7\IDE\TextTransform.exe
IF NOT EXIST "%exe%" SET exe=%msv%\2022\Community\Common7\IDE\TextTransform.exe
:: Default output file will be empty. Use "-out" option to create output file in temp folder.
"%exe%" "%~n0.tt" -out "%TEMP%\%~n0.tt.tmp"
PAUSE
