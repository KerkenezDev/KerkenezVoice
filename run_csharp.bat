@echo off
setlocal enabledelayedexpansion

echo [INFO] Starting Kerkenez Voice...

:: Check for dotnet
where dotnet >nul 2>&1
if %errorlevel% neq 0 (
    if exist "%USERPROFILE%\.dotnet\dotnet.exe" (
        set "DOTNET_CMD=%USERPROFILE%\.dotnet\dotnet.exe"
    ) else (
        echo [ERROR] .NET is not installed or not in PATH.
        pause
        exit /b 1
    )
) else (
    set "DOTNET_CMD=dotnet"
)

:: Run project
"%DOTNET_CMD%" run --project "KerkenezVoice.csproj"
