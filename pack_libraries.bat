@echo off
setlocal enabledelayedexpansion

set "configuration=%~1"
if "%configuration%"=="" set "configuration=Release"

set "projects=src\CodeWF.Log.Core\CodeWF.Log.Core.csproj src\CodeWF.LogViewer.Avalonia\CodeWF.LogViewer.Avalonia.csproj"
set "output_dir=%~dp0publish\nuget\%configuration%"

echo ========================================
echo Building and packing NuGet libraries with %configuration% configuration...
echo ========================================

for %%p in (%projects%) do (
    echo Building %%p...
    dotnet build "%%p" -c %configuration% -nologo /p:GeneratePackageOnBuild=false
    if errorlevel 1 (
        echo Error: Failed to build %%p
        goto :error
    )

    echo Packing %%p...
    dotnet pack "%%p" -c %configuration% --no-build --no-restore -nologo
    if errorlevel 1 (
        echo Error: Failed to pack %%p
        goto :error
    )
)

echo ========================================
echo Library packages created successfully.
echo Output directory: %output_dir%
echo ========================================
if exist "%output_dir%" explorer "%output_dir%"
call :maybe_pause
exit /b 0

:error
echo ========================================
echo Library pack failed. Please check the errors above.
echo ========================================
call :maybe_pause
exit /b 1

:maybe_pause
if /i not "%CODEX_NO_PAUSE%"=="1" pause
exit /b 0
