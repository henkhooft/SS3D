@echo off
rem Hosts a game on this PC (server + your own client). Others join with Start_SS3D_Client.bat.
rem Usage: Start_SS3D_Host.bat [name] [port]. The default name, hostingUser, is ServerOwner in Config\permissions.txt.
setlocal
set "CKEY=%~1"
set "PORT=%~2"
if "%CKEY%"=="" set "CKEY=hostingUser"
if "%PORT%"=="" set "PORT=1151"
cd /d "%~dp0Game"
start "" SS3D.exe -ip=127.0.0.1 -host "-port=%PORT%" "-ckey=%CKEY%" -skipintro
