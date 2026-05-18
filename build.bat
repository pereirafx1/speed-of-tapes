@echo off
setlocal EnableDelayedExpansion

REM ==========================================================================
REM  build.bat — Build SpeedOfTape.dll for ATAS
REM
REM  PREREQUISITES
REM    1. .NET SDK 6.x or later  (https://dotnet.microsoft.com/download)
REM    2. ATAS Platform installed on this machine.
REM
REM  USAGE
REM    build.bat [ATAS_INSTALL_DIR]
REM
REM  EXAMPLES
REM    build.bat
REM    build.bat "C:\Program Files (x86)\ATAS Platform"
REM    build.bat "D:\MyATAS"
REM ==========================================================================

echo.
echo ===  Speed of Tape — ATAS indicator build  ===
echo.

REM --- Locate ATAS installation directory ----------------------------------

set "ATAS_PATH=%~1"

if "%ATAS_PATH%"=="" (
    REM Try common default install locations
    for %%D in (
        "C:\Program Files (x86)\ATAS Platform"
        "C:\Program Files\ATAS Platform"
        "%LOCALAPPDATA%\ATAS Platform"
    ) do (
        if exist "%%~D\ATAS.Indicators.dll" (
            set "ATAS_PATH=%%~D"
            goto :found_atas
        )
    )
    echo [ERROR] Cannot auto-detect ATAS.  Run:
    echo         build.bat "C:\path\to\ATAS Platform"
    exit /b 1
)

:found_atas
echo [INFO]  ATAS path : %ATAS_PATH%

REM Verify that the required DLLs are actually there
set "MISSING=0"
for %%F in (ATAS.Indicators.dll ATAS.Indicators.Other.dll ATAS.Indicators.Technical.dll ATAS.DataFeedsCore.dll) do (
    if not exist "%ATAS_PATH%\%%F" (
        echo [WARN]  Not found: %ATAS_PATH%\%%F
        set "MISSING=1"
    )
)
if "%MISSING%"=="1" (
    echo [ERROR] One or more required DLLs are missing — check the path above.
    exit /b 1
)

REM --- Build ---------------------------------------------------------------

echo.
echo [INFO]  Building SpeedOfTape.dll (Release / net48) ...
dotnet build SpeedOfTape.csproj -c Release --nologo -p:ATASPath="%ATAS_PATH%"

if errorlevel 1 (
    echo.
    echo [ERROR] Build failed — check the output above.
    exit /b 1
)

REM --- Report output -------------------------------------------------------

set "OUT=bin\Release\net48\SpeedOfTape.dll"

if not exist "%OUT%" (
    echo [ERROR] DLL not found at expected path: %OUT%
    exit /b 1
)

echo.
echo [OK]  Build succeeded.
echo       Output : %CD%\%OUT%
echo.
echo === DEPLOYMENT ===
echo  1. Copy  SpeedOfTape.dll  to your ATAS custom indicators folder:
echo       %%APPDATA%%\ATAS Platform\CustomIndicators\
echo  2. Restart ATAS.
echo  3. Open a chart, right-click ^> Add indicator ^> search "Speed of Tape".

endlocal
