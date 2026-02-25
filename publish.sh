#!/bin/bash
set -e

ROOT="/mnt/c/Tools/GMinor"
CONSOLE_DEST="$ROOT/Console"
WPF_DEST="$ROOT/Wpf"
SOLUTION="$(dirname "$0")"

SKIP_TESTS=false
PUBLISH_CONSOLE=true
PUBLISH_WPF=true
TARGET_EXPLICIT=false

for arg in "$@"; do
  case "$arg" in
    --skip-tests) SKIP_TESTS=true ;;
    --console)
      if [ "$TARGET_EXPLICIT" = false ]; then
        PUBLISH_WPF=false
        TARGET_EXPLICIT=true
      fi
      PUBLISH_CONSOLE=true
      ;;
    --gui)
      if [ "$TARGET_EXPLICIT" = false ]; then
        PUBLISH_CONSOLE=false
        TARGET_EXPLICIT=true
      fi
      PUBLISH_WPF=true
      ;;
    *)
      echo "Unknown argument: $arg"
      echo ""
      echo "Usage: $(basename "$0") [--console] [--gui] [--skip-tests]"
      echo ""
      echo "  --console      Publish GMinor.Console only"
      echo "  --gui          Publish GMinor.Wpf only"
      echo "  --skip-tests   Skip running tests before publishing"
      echo "  (no target)    Publish both Console and Wpf"
      exit 1
      ;;
  esac
done

if [ "$SKIP_TESTS" = false ]; then
  echo "Running tests..."
  dotnet test "$SOLUTION" -c Release
fi

publish_with_settings() {
  local project="$1"
  local dest="$2"
  local label="$3"
  local settings="$dest/appsettings.json"

  [ -f "$settings" ] && cp "$settings" "$settings.bak"

  echo "Publishing $label..."
  dotnet publish "$SOLUTION/$project" -r win-x64 --self-contained -c Release -o "$dest"

  if [ -f "$settings.bak" ]; then
    mv "$settings.bak" "$settings"
    echo "Preserved $label appsettings.json."
  fi
}

[ "$PUBLISH_CONSOLE" = true ] && publish_with_settings "GMinor.Console" "$CONSOLE_DEST" "Console"
[ "$PUBLISH_WPF"     = true ] && publish_with_settings "GMinor.Wpf"     "$WPF_DEST"     "Wpf"

echo ""
echo "Done."
[ "$PUBLISH_CONSOLE" = true ] && echo "  Console → C:\\Tools\\GMinor\\Console\\GMinor.Console.exe"
[ "$PUBLISH_WPF"     = true ] && echo "  Wpf     → C:\\Tools\\GMinor\\Wpf\\GMinor.Wpf.exe"
