@echo off
setlocal enabledelayedexpansion

echo ===================================================
echo   Building Kerkenez Voice (Portable Single-File)
echo ===================================================

:: Check for dotnet
where dotnet >nul 2>&1
if %errorlevel% neq 0 (
    if exist "%USERPROFILE%\.dotnet\dotnet.exe" (
        set "DOTNET_CMD=%USERPROFILE%\.dotnet\dotnet.exe"
    ) else (
        echo [ERROR] .NET SDK is not installed or not in PATH.
        pause
        exit /b 1
    )
) else (
    set "DOTNET_CMD=dotnet"
)

echo [INFO] Restoring and publishing portable executable...
"%DOTNET_CMD%" publish "KerkenezVoice.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "publish"

if %errorlevel% equ 0 (
    echo.
    echo [SUCCESS] Portable executable created at: publish\KerkenezVoice.exe
    echo [INFO] Config at %%APPDATA%%\Kerkenez\voice and models at %%LOCALAPPDATA%%\Programs\Kerkenez\voice
    echo.
) else (
    echo [ERROR] Build failed.
)

pause
