@echo off
setlocal

set "ROOT=%~dp0"
set "SOLUTION=%ROOT%CodeWF.Log.slnx"
set "CONFIGURATION=%~1"
if "%CONFIGURATION%"=="" set "CONFIGURATION=Release"

set "PACKAGE_DIR=%ROOT%artifacts\packages"
set "PROJECT_CORE=%ROOT%src\CodeWF.Log.Core\CodeWF.Log.Core.csproj"
set "PROJECT_EXTENSIONS=%ROOT%src\CodeWF.Log.Extensions.Logging\CodeWF.Log.Extensions.Logging.csproj"
set "PROJECT_AVALONIA=%ROOT%src\CodeWF.Log.Avalonia\CodeWF.Log.Avalonia.csproj"

echo [CodeWF.LogViewer] Restore packages...
dotnet restore "%SOLUTION%"
if errorlevel 1 goto :failed

if not exist "%PACKAGE_DIR%" mkdir "%PACKAGE_DIR%"
del /q "%PACKAGE_DIR%\*.nupkg" 2>nul
del /q "%PACKAGE_DIR%\*.snupkg" 2>nul

echo [CodeWF.LogViewer] Build %CONFIGURATION% packages...
dotnet build "%PROJECT_CORE%" -c "%CONFIGURATION%" --no-restore -nologo /p:GeneratePackageOnBuild=false
if errorlevel 1 goto :failed
dotnet build "%PROJECT_EXTENSIONS%" -c "%CONFIGURATION%" --no-restore -nologo /p:GeneratePackageOnBuild=false
if errorlevel 1 goto :failed
dotnet build "%PROJECT_AVALONIA%" -c "%CONFIGURATION%" --no-restore -nologo /p:GeneratePackageOnBuild=false
if errorlevel 1 goto :failed

echo [CodeWF.LogViewer] Pack NuGet packages...
dotnet pack "%PROJECT_CORE%" -c "%CONFIGURATION%" --no-build --no-restore -nologo -o "%PACKAGE_DIR%"
if errorlevel 1 goto :failed
dotnet pack "%PROJECT_EXTENSIONS%" -c "%CONFIGURATION%" --no-build --no-restore -nologo -o "%PACKAGE_DIR%"
if errorlevel 1 goto :failed
dotnet pack "%PROJECT_AVALONIA%" -c "%CONFIGURATION%" --no-build --no-restore -nologo -o "%PACKAGE_DIR%"
if errorlevel 1 goto :failed

if not exist "%PACKAGE_DIR%\*.nupkg" goto :failed

echo.
echo [CodeWF.LogViewer] Packages:
dir /b "%PACKAGE_DIR%\*.nupkg"
echo.
echo [CodeWF.LogViewer] Done. Output: %PACKAGE_DIR%
exit /b 0

:failed
echo.
echo [CodeWF.LogViewer] Pack failed.
exit /b 1
