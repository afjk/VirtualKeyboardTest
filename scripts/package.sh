#!/usr/bin/env bash
# Collect libmozc.so (Android arm64-v8a), mozc.data and licence notices into ./dist,
# and verify the shared library. Run after the Bazel builds in mozc/src.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
src="$root/mozc/src"
dist="$root/dist"
bazel="${BAZEL:-bazelisk}"
android_flags=(--config oss_android --config release_build)

rm -rf "$dist"
mkdir -p "$dist/arm64-v8a" "$dist/licenses"
cd "$src"

so="$("$bazel" cquery //android/jni:mozc.arm64 "${android_flags[@]}" --output=files 2>/dev/null | grep '\.so$' | head -n1)"
data="$("$bazel" cquery //data_manager/oss:mozc_dataset_for_oss --output=files 2>/dev/null | grep '/mozc\.data$' | head -n1)"
[ -f "$so" ] || { echo "libmozc.so not found: '$so'" >&2; exit 1; }
[ -f "$data" ] || { echo "mozc.data not found: '$data'" >&2; exit 1; }
cp "$so" "$dist/arm64-v8a/libmozc.so"
cp "$data" "$dist/mozc.data"

# --- Verification of the Android library ---
lib="$dist/arm64-v8a/libmozc.so"
file "$lib" | tee "$dist/verify.txt"
grep -q 'ARM aarch64' "$dist/verify.txt" || { echo "not an aarch64 ELF" >&2; exit 1; }
echo "--- NEEDED" | tee -a "$dist/verify.txt"
readelf -dW "$lib" | grep NEEDED | tee -a "$dist/verify.txt"
echo "--- LOAD alignment (Android 15+ requires 16 KB pages)" | tee -a "$dist/verify.txt"
readelf -lW "$lib" | awk '$1=="LOAD"{print $NF}' | tee -a "$dist/verify.txt"
if readelf -lW "$lib" | awk '$1=="LOAD"{print $NF}' | grep -qv '^0x4000$'; then
  echo "LOAD segment not aligned to 16 KB" >&2; exit 1
fi
echo "--- JNI entry point" | tee -a "$dist/verify.txt"
readelf -W --dyn-syms "$lib" | grep -o 'Java_com_google_android_apps_inputmethod_libs_mozc_session_MozcJni_initialize' \
  | tee -a "$dist/verify.txt" | grep -q . || { echo "JNI entry point missing" >&2; exit 1; }

# --- Licence notices for everything inside the binaries ---
cp "$root/mozc/LICENSE" "$dist/licenses/mozc-LICENSE.txt"
cp "$src/data/dictionary_oss/README.txt" "$dist/licenses/mozc-dictionary_oss-README.txt"
external="$("$bazel" info output_base 2>/dev/null)/external"
for pattern in 'abseil-cpp*' 'protobuf*' 'ja_usage_dict*' 'zlib*' 'jsoncpp*'; do
  for dir in "$external"/$pattern; do
    [ -d "$dir" ] || continue
    for f in "$dir"/LICENSE* "$dir"/COPYING*; do
      [ -f "$f" ] && cp "$f" "$dist/licenses/$(basename "$dir")-$(basename "$f").txt"
    done
  done
done

{
  echo "mozc_commit: $(git -C "$root/mozc" rev-parse HEAD)"
  echo "bazel: $("$bazel" --version 2>/dev/null)"
  echo "ndk: $(grep -m1 Pkg.Revision "$src"/third_party/ndk/*/source.properties 2>/dev/null || echo unknown)"
  echo "built_at_utc: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "--- sizes"
  ls -l "$lib" "$dist/mozc.data" | awk '{print $5, $NF}'
} > "$dist/BUILD_INFO.txt"
(cd "$dist" && find . -type f ! -name SHA256SUMS -print0 | sort -z | xargs -0 sha256sum > SHA256SUMS)
cat "$dist/BUILD_INFO.txt"
ls -R "$dist/licenses"
