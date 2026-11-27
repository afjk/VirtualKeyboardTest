#!/usr/bin/env bash
# Collect the shim libraries, mozc.data and licence notices into ./dist and verify
# them. Run after the Bazel builds in mozc/src (see .github/workflows/build.yml).
#
# dist/
#   arm64-v8a/libmozc_shim.so   Android arm64, stripped
#   x86_64/libmozc_shim.so      Linux x64 (Unity Editor on Linux), stripped
#   mozc.data                   OSS dictionary data
#   licenses/                   notices for everything inside the binaries
#   symbols/arm64-v8a/libmozc_shim.so  unstripped, for symbolising crashes
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
src="$root/mozc/src"
dist="$root/dist"
bazel="${BAZEL:-bazelisk}"
android_flags=(--config oss_android --config release_build)
# Must match the flags of the Linux build step in .github/workflows/build.yml.
host_flags=(--compilation_mode=opt)

rm -rf "$dist"
mkdir -p "$dist/arm64-v8a" "$dist/x86_64" "$dist/licenses" "$dist/symbols/arm64-v8a"
cd "$src"

files() { "$bazel" cquery "$@" --output=files 2>/dev/null; }
android_so="$(files //android/jni:libmozc_shim.arm64 "${android_flags[@]}" | grep '\.so$' | head -n1)"
host_so="$(files //android/jni:libmozc_shim.so "${host_flags[@]}" | grep '\.so$' | head -n1)"
data="$(files //data_manager/oss:mozc_dataset_for_oss "${host_flags[@]}" | grep '/mozc\.data$' | head -n1)"
for f in "$android_so" "$host_so" "$data"; do
  [ -f "$f" ] || { echo "build output not found: '$f'" >&2; exit 1; }
done

strip_tool="$(ls "$src"/third_party/ndk/*/toolchains/llvm/prebuilt/linux-x86_64/bin/llvm-strip | head -n1)"
cp "$android_so" "$dist/symbols/arm64-v8a/libmozc_shim.so"
"$strip_tool" --strip-unneeded -o "$dist/arm64-v8a/libmozc_shim.so" "$android_so"
"$strip_tool" --strip-unneeded -o "$dist/x86_64/libmozc_shim.so" "$host_so"
cp "$data" "$dist/mozc.data"
chmod 0644 "$dist"/*/*.so "$dist/symbols/arm64-v8a/libmozc_shim.so"

# --- Verification ---
report="$dist/verify.txt"
: > "$report"
log() { echo "$*" | tee -a "$report"; }
check_lib() {
  local lib="$1" arch="$2"
  log "=== $lib"
  file "$lib" | tee -a "$report"
  file "$lib" | grep -q "$arch" || { echo "unexpected architecture" >&2; exit 1; }
  log "--- NEEDED"
  readelf -dW "$lib" | grep NEEDED | tee -a "$report"
  log "--- LOAD alignment"
  readelf -lW "$lib" | awk '$1=="LOAD"{print $NF}' | tee -a "$report"
  log "--- exported C API"
  for sym in mozc_shim_initialize mozc_shim_eval mozc_shim_free mozc_shim_data_version mozc_shim_last_error; do
    readelf -W --dyn-syms "$lib" | grep -qw "$sym" || { echo "missing export $sym in $lib" >&2; exit 1; }
    log "$sym"
  done
}
check_lib "$dist/arm64-v8a/libmozc_shim.so" 'ARM aarch64'
check_lib "$dist/x86_64/libmozc_shim.so" 'x86-64'
if readelf -lW "$dist/arm64-v8a/libmozc_shim.so" | awk '$1=="LOAD"{print $NF}' | grep -qv '^0x4000$'; then
  echo "Android library LOAD segment not aligned to 16 KB" >&2; exit 1
fi

# --- Licence notices ---
cp "$root/mozc/LICENSE" "$dist/licenses/mozc-LICENSE.txt"
cp "$src/data/dictionary_oss/README.txt" "$dist/licenses/mozc-dictionary_oss-README.txt"
external="$("$bazel" info output_base 2>/dev/null)/external"
for name in abseil-cpp protobuf zlib ja_usage_dict; do
  found=0
  for dir in "$external"/*"$name"*; do
    [ -d "$dir" ] || continue
    for f in "$dir"/LICENSE* "$dir"/COPYING* "$dir"/README*; do
      [ -f "$f" ] || continue
      # README files are kept only where they carry the licence (usage dictionary).
      case "$(basename "$f")" in README*) [ "$name" = ja_usage_dict ] || continue ;; esac
      cp "$f" "$dist/licenses/$name-$(basename "$f").txt"
      found=1
    done
  done
  [ "$found" = 1 ] || echo "WARNING: no licence file found for $name" | tee -a "$report"
done

{
  echo "mozc_commit: $(git -C "$root/mozc" rev-parse HEAD)"
  echo "bazel: $("$bazel" --version 2>/dev/null)"
  echo "ndk: $(grep -m1 Pkg.Revision "$src"/third_party/ndk/*/source.properties 2>/dev/null || echo unknown)"
  echo "built_at_utc: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "--- sizes (bytes)"
  (cd "$dist" && ls -l arm64-v8a/libmozc_shim.so x86_64/libmozc_shim.so mozc.data symbols/arm64-v8a/libmozc_shim.so | awk '{print $5, $NF}')
} > "$dist/BUILD_INFO.txt"
(cd "$dist" && find . -type f ! -name SHA256SUMS -print0 | sort -z | xargs -0 sha256sum > SHA256SUMS)
cat "$dist/BUILD_INFO.txt"
ls "$dist/licenses"
