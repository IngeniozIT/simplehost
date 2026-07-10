#!/usr/bin/env bash
#
# Packages a self-contained, double-clickable Windows build of SimpleHost.
#
# Produces dist/SimpleHost/ containing:
#   - SimpleHost.exe            the app, runtime bundled (no .NET install needed on Windows)
#   - hosts.sample              a sample hosts file to try it against
#   - Run SimpleHost (sample).cmd   double-click this to launch against the sample file
#
# Copy the whole dist/SimpleHost folder to your Windows machine and double-click the .cmd.
# SimpleHost is a tray-first app: after launch, open the window from its system-tray icon
# ("Open SimpleHost"). To edit the real system hosts file instead, right-click SimpleHost.exe
# and "Run as administrator" (with no --hosts-file argument).
#
set -euo pipefail
cd "$(dirname "$0")/.."

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

OUT="dist/SimpleHost"
rm -rf "$OUT"

dotnet publish src/SimpleHost.Presentation/SimpleHost.Presentation.csproj \
  -c Release -r win-x64 --self-contained \
  -p:EnableWindowsTargeting=true \
  -o "$OUT"

cp samples/hosts.sample "$OUT/hosts.sample"

# Double-clickable launcher. %~dp0 is the folder the .cmd lives in (a Windows path),
# so the exe and sample resolve wherever the folder is copied. Written with CRLF line
# endings for Windows batch.
printf '@echo off\r\nstart "" "%%~dp0SimpleHost.exe" --hosts-file "%%~dp0hosts.sample"\r\n' \
  > "$OUT/Run SimpleHost (sample).cmd"

echo
echo "Packaged into $OUT/"
ls -1 "$OUT" | sed 's/^/  /'
