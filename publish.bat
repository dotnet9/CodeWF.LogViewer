@echo off
setlocal enabledelayedexpansion

if "%~1"=="" (
    set "project_paths=src\ConsoleLogDemo src\AvaloniaLogDemo"
) else (
    set "project_paths=%~1"
)

if "%~2"=="" (
    set "platforms=win-x64 win-x86 linux-x64 linux-arm64"
) else (
    set "platforms=%~2"
)

rem Optional 3rd arg: space-separated TFMs aligned with project_paths.
rem Use "default" to keep the platform default TFM for that project.
set "target_frameworks=%~3"
set "publish_root=%~dp0publish"
set "props_file=%~dp0Directory.Build.props"
set "props_backup=%TEMP%\CodeWF.LogViewer.Directory.Build.%RANDOM%%RANDOM%.props.bak"

if exist "%props_file%" (
    copy /y "%props_file%" "%props_backup%" >nul
)

for %%p in (%platforms%) do (
    set "default_tfm="
    set "default_pubxml="
    set "rid=%%p"

    if "%%p"=="linux-x64" set "default_tfm=net11.0" & set "default_pubxml=FolderProfile_linux-x64.pubxml"
    if "%%p"=="linux-arm64" set "default_tfm=net11.0" & set "default_pubxml=FolderProfile_linux-arm64.pubxml"
    if "%%p"=="win-x64" set "default_tfm=net11.0-windows" & set "default_pubxml=FolderProfile_win-x64.pubxml"
    if "%%p"=="win-x86" set "default_tfm=net11.0-windows" & set "default_pubxml=FolderProfile_win-x86.pubxml"

    if not defined default_tfm (
        echo Error: Unsupported platform %%p
        goto :error
    )

    set "remaining_tfms=%target_frameworks%"

    echo ========================================
    echo Building %%p...
    echo ========================================

    for %%f in (GlobalAssemblies\*) do (
        echo Updating assembly version for %%f...
        powershell -ExecutionPolicy Bypass -File "UpdateAssemblyVersion.ps1" -AssemblyInfoFile "%%f" -Configuration "Release" -Platform "%%p"
        if errorlevel 1 (
            echo Error: Failed to update assembly version for %%f
            goto :error
        )
    )

    powershell -ExecutionPolicy Bypass -File "SetPlatformMacro.ps1" -Platform "%%p"
    if errorlevel 1 (
        echo Error: Failed to set platform macro
        goto :error
    )

    for %%d in (%project_paths%) do (
        set "project_tfm=!default_tfm!"
        if defined target_frameworks (
            if not defined remaining_tfms (
                echo Error: Missing target framework for %%d
                goto :error
            )
            for /f "tokens=1*" %%a in ("!remaining_tfms!") do (
                set "project_tfm=%%a"
                set "remaining_tfms=%%b"
            )
            if /i "!project_tfm!"=="default" set "project_tfm=!default_tfm!"
        )

        set "project_pubxml=!default_pubxml!"

        set "publish_profile=%%d\Properties\PublishProfiles\!project_pubxml!"
        for %%n in ("%%d") do set "project_name=%%~nxn"
        set "publish_output=!publish_root!\%%p\!project_name!"

        echo Publishing %%d for %%p with !project_tfm!...
        if exist "!publish_profile!" (
            dotnet publish "%%d" -f !project_tfm! /p:PublishProfile="!publish_profile!"
        ) else (
            echo Publish profile not found, falling back to CLI publish options: !publish_profile!
            set "publish_args=-c Release -f !project_tfm! -r !rid! --self-contained true -o ""!publish_output!"""
            set "publish_args=!publish_args! /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:PublishTrimmed=true"
            if /i "!project_name!"=="AvaloniaLogDemo" (
                set "publish_args=!publish_args! /p:IsTrimmable=true /p:TrimMode=partial"
                set "publish_args=!publish_args! /p:TrimIncludeTestAssemblies=false /p:DebugType=none /p:DebugSymbols=false"
                set "publish_args=!publish_args! /p:PublishReadyToRunShowWarnings=false /p:EnableCompiledBindings=true"
                if /i "!rid:~0,4!"=="win-" set "publish_args=!publish_args! /p:PublishAot=true"
                if /i not "!rid:~0,4!"=="win-" set "publish_args=!publish_args! /p:PublishReadyToRun=false"
            )
            dotnet publish "%%d" !publish_args!
        )
        if errorlevel 1 (
            echo Error: Failed to publish %%d for %%p
            goto :error
        )
    )
    echo.
)

echo ========================================
echo All platforms published successfully.
echo ========================================
call :restore_props
echo Removing *.pdb files...
if exist "%~dp0publish" (
    for /r "%~dp0publish" %%f in (*.pdb) do del /q "%%f" 2>nul
    echo *.pdb files removed.
)
explorer "%~dp0publish"
call :maybe_pause
goto :eof

:error
call :restore_props
echo ========================================
echo Build failed. Please check the errors above.
echo ========================================
call :maybe_pause
exit /b 1

:restore_props
if exist "%props_backup%" (
    copy /y "%props_backup%" "%props_file%" >nul
    del /q "%props_backup%" >nul 2>nul
)
exit /b 0

:maybe_pause
if /i not "%CODEX_NO_PAUSE%"=="1" pause
exit /b 0
