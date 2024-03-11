@echo off
cd /d "%~dp0"
:: Activate "tuning" Python environment.
start powershell -ExecutionPolicy Bypass -NoExit -command "tuning\Scripts\activate"
exit