#!/usr/bin/env bash
# Build ./unity-dist/Assets: unity/Assets plus the binaries from ./dist.
# Copy unity-dist/Assets into a Unity project (Unity 2021.3 or later).
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
dist="$root/dist"
out="$root/unity-dist"
rm -rf "$out"
mkdir -p "$out"
cp -R "$root/unity/Assets" "$out/Assets"
plugins="$out/Assets/MozcShim/Plugins"
cp "$dist/arm64-v8a/libmozc_shim.so" "$plugins/Android/arm64-v8a/"
cp "$dist/x86_64/libmozc_shim.so" "$plugins/x86_64/"
mkdir -p "$out/Assets/StreamingAssets/mozc"
cp "$dist/mozc.data" "$out/Assets/StreamingAssets/mozc/"
mkdir -p "$out/Assets/MozcShim/Licenses"
cp "$dist"/licenses/* "$out/Assets/MozcShim/Licenses/"
cp "$dist/BUILD_INFO.txt" "$dist/verify.txt" "$out/Assets/MozcShim/"
find "$out" -type f | sort | sed "s#^$out/##"
