@echo off
setlocal enabledelayedexpansion

set "project_paths=src\ConsoleLogDemo src\AvaloniaLogDemo"
set "platforms=win-x64"
set "target_frameworks=net10.0 default"

call "%~dp0publish.bat" "%project_paths%" "%platforms%" "%target_frameworks%"
