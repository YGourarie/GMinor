#!/bin/bash
set -e

DEST="/mnt/c/Tools/GMinor"
SOLUTION="$(dirname "$0")"
PROJECT="$SOLUTION/GMinor.Console"

SKIP_TESTS=false
for arg in "$@"; do
  [[ "$arg" == "--skip-tests" ]] && SKIP_TESTS=true
done

if [ "$SKIP_TESTS" = false ]; then
  echo "Running tests..."
  dotnet test "$SOLUTION" -c Release
fi

APPSETTINGS="$DEST/appsettings.json"

# If a live appsettings.json already exists, preserve it across the publish.
if [ -f "$APPSETTINGS" ]; then
  cp "$APPSETTINGS" "$APPSETTINGS.bak"
fi

echo "Publishing..."
dotnet publish "$PROJECT" -r win-x64 --self-contained -c Release -o "$DEST"

if [ -f "$APPSETTINGS.bak" ]; then
  mv "$APPSETTINGS.bak" "$APPSETTINGS"
  echo "Preserved existing appsettings.json (not overwritten)."
fi

echo "Done. Files copied to C:\Tools\GMinor"
