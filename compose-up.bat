@echo off
setlocal

cd /d "%~dp0"

docker compose build
if errorlevel 1 exit /b %errorlevel%

docker compose up -d
exit /b %errorlevel%
