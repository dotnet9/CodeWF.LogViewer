@echo off
setlocal enabledelayedexpansion

set "project_paths=src\ConsoleLogDemo src\AvaloniaLogDemo"
set "platforms=win-x64 win-x86 linux-x64 linux-arm64"
set "target_frameworks=net11.0 default"

call "%~dp0publish.bat" "%project_paths%" "%platforms%" "%target_frameworks%"
