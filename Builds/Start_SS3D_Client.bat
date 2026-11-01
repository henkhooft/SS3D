@echo off
rem Joins an SS3D server. Usage: Start_SS3D_Client.bat [name] [server ip] [port]
rem With no arguments it asks for your name and the server address.
setlocal
set "CKEY=%~1"
set "SERVER_IP=%~2"
set "PORT=%~3"
if "%CKEY%"=="" set /p "CKEY=Player name [%USERNAME%]: "
if "%CKEY%"=="" set "CKEY=%USERNAME%"
if "%SERVER_IP%"=="" set /p "SERVER_IP=Server IP [127.0.0.1]: "
if "%SERVER_IP%"=="" set "SERVER_IP=127.0.0.1"
if "%PORT%"=="" set "PORT=1151"
cd /d "%~dp0Game"
start "" SS3D.exe "-ip=%SERVER_IP%" "-port=%PORT%" "-ckey=%CKEY%" -skipintro
