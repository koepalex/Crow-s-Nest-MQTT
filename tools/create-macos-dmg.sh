#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 3 || $# -gt 4 ]]; then
  echo "Usage: $0 <publish-dir> <runtime> <version> [executable-name]"
  exit 1
fi

publish_dir="$1"
runtime="$2"
version="$3"
provided_executable_name="${4:-}"

if [[ ! -d "$publish_dir" ]]; then
  echo "Publish directory does not exist: $publish_dir"
  exit 1
fi

staging_dir="$(mktemp -d)"
dmg_name="crows-nest-mqtt-${runtime}-${version}.dmg"
dmg_path="$publish_dir/$dmg_name"
volume_name="CrowsNestMQTT"

cleanup() {
  rm -rf "$staging_dir"
}

trap cleanup EXIT

shopt -s nullglob
app_bundles=("$publish_dir"/*.app)
shopt -u nullglob

if [[ ${#app_bundles[@]} -gt 1 ]]; then
  echo "Multiple .app bundles found in: $publish_dir"
  exit 1
fi

if [[ ${#app_bundles[@]} -eq 1 ]]; then
  app_bundle="${app_bundles[0]}"
  app_name="$(basename "$app_bundle")"
  cp -R "$app_bundle" "$staging_dir/$app_name"
else
  executable_name="$provided_executable_name"
  if [[ -z "$executable_name" ]]; then
    executables=()
    for candidate in "$publish_dir"/*; do
      if [[ ! -f "$candidate" || ! -x "$candidate" ]]; then
        continue
      fi

      base_name="$(basename "$candidate")"
      case "$base_name" in
        *.dylib|*.so|*.pdb|*.xml|*.json|*.deps.json|*.runtimeconfig.json)
          continue
          ;;
        lib*)
          continue
          ;;
      esac

      executables+=("$base_name")
    done

    if [[ ${#executables[@]} -eq 0 ]]; then
      echo "Could not find a runnable executable in: $publish_dir"
      echo "Pass executable name as 4th argument to this script."
      exit 1
    fi

    if [[ ${#executables[@]} -gt 1 ]]; then
      echo "Found multiple candidate executables in: $publish_dir"
      printf ' - %s\n' "${executables[@]}"
      echo "Pass executable name as 4th argument to this script."
      exit 1
    fi

    executable_name="${executables[0]}"
  fi

  if [[ ! -f "$publish_dir/$executable_name" ]]; then
    echo "Executable not found: $publish_dir/$executable_name"
    exit 1
  fi

  app_name="CrowsNestMQTT.app"
  app_root="$staging_dir/$app_name"
  app_contents="$app_root/Contents"
  app_macos="$app_contents/MacOS"

  mkdir -p "$app_macos"

  shopt -s nullglob dotglob
  for entry in "$publish_dir"/*; do
    entry_name="$(basename "$entry")"
    if [[ "$entry_name" == *.dmg ]]; then
      continue
    fi
    if [[ "$entry_name" == *.app ]]; then
      continue
    fi
    cp -R "$entry" "$app_macos/$entry_name"
  done
  shopt -u nullglob dotglob

  cat > "$app_contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleExecutable</key>
  <string>${executable_name}</string>
  <key>CFBundleIdentifier</key>
  <string>com.koepalex.crowsnestmqtt</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>CrowsNestMQTT</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>${version}</string>
  <key>CFBundleVersion</key>
  <string>${version}</string>
  <key>LSMinimumSystemVersion</key>
  <string>11.0</string>
</dict>
</plist>
EOF
fi

ln -s /Applications "$staging_dir/Applications"

hdiutil create \
  -volname "$volume_name" \
  -srcfolder "$staging_dir" \
  -ov \
  -format UDZO \
  "$dmg_path"

echo "Created DMG: $dmg_path"
