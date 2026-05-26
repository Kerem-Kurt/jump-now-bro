#!/usr/bin/env bash
# Fails if any asset under Assets/ lacks a paired .meta, or any .meta is orphaned.
# Unity ignores dot-prefixed paths and "~" backups, so those are skipped.
set -uo pipefail

status=0

# Assets (files + dirs) missing their .meta sibling.
while IFS= read -r -d '' f; do
  case "$f" in */.*) continue ;; esac
  if [ ! -e "${f}.meta" ]; then
    echo "MISSING .meta for: $f"
    status=1
  fi
done < <(find Assets -mindepth 1 \( -type f -o -type d \) ! -name '*.meta' ! -name '*~' -print0)

# Orphaned .meta files (the asset they describe is gone).
while IFS= read -r -d '' m; do
  asset="${m%.meta}"
  if [ ! -e "$asset" ]; then
    echo "ORPHAN .meta (no matching asset): $m"
    status=1
  fi
done < <(find Assets -name '*.meta' -print0)

if [ "$status" -eq 0 ]; then
  echo "OK: every asset and .meta is paired."
fi
exit "$status"
