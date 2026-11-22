#!/usr/bin/env bash
# Build, render and encode the intro film.
#   video/blender/make_film.sh preview   # 960x540, 4 samples, every 2nd frame -> out/preview/digitalheat-intro-preview.mp4
#   video/blender/make_film.sh final     # 1920x1080, 64 samples, all frames  -> out/final/digitalheat-intro.mp4
# BLENDER: path to a Blender 4.x executable (default: blender), or a Python 3.11 with the bpy module.
set -euo pipefail
PRESET="${1:-preview}"
cd "$(dirname "$0")"
BLENDER="${BLENDER:-blender}"
PY="${PYTHON:-python3}"
[ -f data/film-data.json ] && [ -f numbers.json ] || "$PY" prepare_data.py
case "$(basename "$BLENDER")" in
  python*) "$BLENDER" render.py --preset "$PRESET" ;;
  *)       "$BLENDER" -b --factory-startup -P render.py -- --preset "$PRESET" ;;
esac
"$PY" post.py --preset "$PRESET"
