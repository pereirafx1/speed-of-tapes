#!/usr/bin/env bash
# =============================================================================
# build.sh — Stub-mode build for CI / syntax verification
#
# Because ATAS is Windows-only software, the stub build is the only option on
# Linux/macOS.  The resulting DLL will NOT load inside ATAS — it is useful
# only to confirm that the C# code compiles cleanly.
#
# PREREQUISITES
#   dotnet SDK 6.x+   (https://dotnet.microsoft.com/download)
#
# USAGE
#   chmod +x build.sh && ./build.sh
# =============================================================================

set -euo pipefail

echo
echo "===  Speed of Tape — stub build (compile verification only)  ==="
echo

dotnet build SpeedOfTape.csproj -c Release -p:UseStubs=true --nologo

OUT="bin/Release/net48/SpeedOfTape.dll"

if [[ -f "$OUT" ]]; then
    echo
    echo "[OK]  Stub build succeeded: $PWD/$OUT"
    echo
    echo "NOTE: This DLL was compiled against stub types and will NOT load"
    echo "      in ATAS.  Run build.bat on a Windows machine with ATAS"
    echo "      installed to produce the deployable DLL."
else
    echo "[ERROR] Expected output not found: $OUT"
    exit 1
fi
