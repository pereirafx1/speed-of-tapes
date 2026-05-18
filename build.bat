@echo off
setlocal EnableDelayedExpansion

REM ==========================================================================
REM  build.bat — Build SpeedOfTape.dll for ATAS
REM
REM  PREREQUISITES
REM    1. .NET SDK 6.x or later  (https://dotnet.microsoft.com/download)
REM       Note: even though the target framework is net48, the SDK-style build
REM       toolchain is part of .NET 6+ SDK.
REM    2. ATAS Platform installed on this machine.
REM
REM  USAGE
REM    build.bat [ATAS_BIN_PATH]
REM
REM  EXAMPLE
REM    build.bat "C:\Program Files\ATAS Platform\bin"
REM
REM  If no path is supplied the script tries to auto-detect ATAS.
REM ==========================================================================

echo.
echo ===  Speed of Tape — ATAS indicator build  ===
echo.

REM --- Locate ATAS bin directory -------------------------------------------

set "ATAS_PATH=%~1"

if "%ATAS_PATH%"=="" (
    REM Common default install locations
    for %%D in (
        "C:\Program Files\ATAS Platform\bin"
        "C:\Program Files (x86)\ATAS Platform\bin"
        "%LOCALAPPDATA%\ATAS Platform\bin"
    ) do (
        if exist "%%~D\ATAS.Indicators.dll" (
            set "ATAS_PATH=%%~D"
            goto :found_atas
        )
    )
    echo [ERROR] Cannot auto-detect ATAS.  Run:
    echo         build.bat "C:\path\to\ATAS Platform\bin"
    exit /b 1
)

:found_atas
echo [INFO]  ATAS bin path : %ATAS_PATH%

REM --- Copy SDK DLLs into libs\ --------------------------------------------

if not exist "libs\" mkdir libs

set "COPY_OK=1"
for %%F in (ATAS.Indicators.dll OFT.Rendering.dll) do (
    if exist "%ATAS_PATH%\%%F" (
        echo [INFO]  Copying %%F
        copy /Y "%ATAS_PATH%\%%F" "libs\%%F" >nul
    ) else (
        echo [WARN]  %%F not found at %ATAS_PATH% — build may fail
        set "COPY_OK=0"
    )
)

REM --- Build ---------------------------------------------------------------

echo.
echo [INFO]  Building SpeedOfTape.dll (Release / net48) ...
dotnet build SpeedOfTape.csproj -c Release --nologo

if errorlevel 1 (
    echo.
    echo [ERROR] Build failed.  Check the output above for errors.
    exit /b 1
)

REM --- Report output location ----------------------------------------------

set "OUT=bin\Release\net48\SpeedOfTape.dll"

if exist "%OUT%" (
    echo.
    echo [OK]    Build succeeded.
    echo         Output : %CD%\%OUT%
    echo.
    echo ===  DEPLOYMENT  ===
    echo  Copy  SpeedOfTape.dll  to your ATAS custom indicators folder, e.g.:
    echo    %%APPDATA%%\ATAS Platform\CustomIndicators\
    echo  Then restart ATAS and search for "Speed of Tape" in the indicator list.
) else (
    echo [ERROR] DLL not found at expected path: %OUT%
    exit /b 1
)

endlocal
