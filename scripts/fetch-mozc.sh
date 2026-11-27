#!/usr/bin/env bash
# Fetch google/mozc at the commit pinned in MOZC_COMMIT into ./mozc.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
commit="$(tr -d '[:space:]' < "$root/MOZC_COMMIT")"
dest="$root/mozc"

if [ -d "$dest/.git" ] && [ "$(git -C "$dest" rev-parse HEAD)" = "$commit" ]; then
  echo "mozc already at $commit"
  exit 0
fi
rm -rf "$dest"
git init -q "$dest"
git -C "$dest" remote add origin https://github.com/google/mozc.git
git -C "$dest" fetch -q --depth 1 origin "$commit"
git -C "$dest" checkout -q --detach FETCH_HEAD
echo "mozc checked out at $(git -C "$dest" rev-parse HEAD)"
