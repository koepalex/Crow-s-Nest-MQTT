#!/usr/bin/env bash
set -euo pipefail

if [[ $# -gt 2 ]]; then
  echo "Usage: $0 [version] [configuration]"
  exit 1
fi

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This script only runs on macOS."
  exit 1
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"

arch="$(uname -m)"
case "$arch" in
  arm64|aarch64)
    runtime="osx-arm64"
    ;;
  x86_64)
    runtime="osx-x64"
    ;;
  *)
    echo "Unsupported macOS architecture: $arch"
    exit 1
    ;;
esac

version="${1:-0.0.0-local}"
configuration="${2:-Release}"
output_dir="$repo_root/publish/$runtime"

echo "Publishing for runtime: $runtime"
echo "Version: $version"
echo "Configuration: $configuration"

dotnet restore "$repo_root/src/MainApp/CrowsNestMqtt.App.csproj"

dotnet publish "$repo_root/src/MainApp/CrowsNestMqtt.App.csproj" \
  --configuration "$configuration" \
  --runtime "$runtime" \
  --self-contained \
  -p:PublishSingleFile=true \
  -p:Version="$version" \
  -o "$output_dir" \
  --no-restore

"$script_dir/create-macos-dmg.sh" "$output_dir" "$runtime" "$version" "CrowsNestMqtt.App"

echo "Done. DMG should be in: $output_dir"
