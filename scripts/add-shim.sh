#!/usr/bin/env bash
# Copy the C API shim into Mozc's //android/jni package (idempotent).
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
jni="$root/mozc/src/android/jni"
cp "$root/native/mozc_shim.h" "$root/native/mozc_shim.cc" "$root/native/mozc_shim_smoke.cc" "$jni/"
if ! grep -q 'name = "mozc_shim_lib"' "$jni/BUILD.bazel"; then
  cat "$root/native/BUILD.append.bazel" >> "$jni/BUILD.bazel"
fi
echo "shim added to $jni"
