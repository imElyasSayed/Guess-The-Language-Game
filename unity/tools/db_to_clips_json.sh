#!/usr/bin/env bash
# db_to_clips_json.sh — derive the SIMPLE clips.json manifest from the prep pipeline's game.db.
#
# The single-player core reads StreamingAssets/clips.json (JsonClipCatalog). The prep pipeline
# (../../prep) produces out/game.db with a `clips` table (brief §7). This script exports that
# table to the manifest shape the game expects, aliasing snake_case columns (lang_id) to the
# camelCase field names JsonUtility deserializes (langId).
#
# Keep this the single source of truth so game.db and clips.json never drift. When the game
# swaps to reading game.db directly via a Unity SQLite plugin (brief §7 preferred path), this
# step and clips.json go away.
#
# Usage:
#   ./db_to_clips_json.sh /path/to/prep/out/game.db ../Assets/StreamingAssets/clips.json
#
set -euo pipefail

DB="${1:-../../prep/out/game.db}"
OUT="${2:-../Assets/StreamingAssets/clips.json}"

if [ ! -f "$DB" ]; then
  echo "error: db not found: $DB" >&2
  exit 1
fi

sqlite3 "$DB" "
SELECT json_group_array(json_object(
  'id',            id,
  'file',          file,
  'langId',        lang_id,
  'language',      language,
  'country',       country,
  'continent',     continent,
  'transcription', transcription,
  'difficulty',    difficulty
)) FROM clips;
" > "$OUT"

echo "wrote $(sqlite3 "$DB" 'SELECT count(*) FROM clips;') clip(s) -> $OUT"
echo "reminder: also copy the prep out/clips/*.ogg into Assets/StreamingAssets/clips/"
echo "          and out/forbidden/*.json into Assets/StreamingAssets/forbidden/"
