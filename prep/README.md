# Say Again? — Data Prep Pipeline (offline, build-time only)

Turns `languages.json` (hand-curated metadata + forbidden words) + Google FLEURS
audio into the game's shipped data. NOT shipped in the game; DuckDB & ffmpeg are
prep tools only.

## Prereqs
- Python 3.11+ (no ffmpeg needed — encoding uses libsndfile via the `soundfile` package)
- `python -m venv .venv && . .venv/bin/activate && pip install -r requirements.txt`

## Run (real FLEURS pull)
- `python -m prep.run_prep languages.json out`
- Outputs into `out/`:
  - `clips/` — Ogg Vorbis clips (`<lang_id>_<n>.ogg`)
  - `game.db` — SQLite `clips` table (ship in Unity StreamingAssets)
  - `forbidden/<lang_id>.json` — per-language fact sheets for the oracle relay

## Test
- `python -m pytest` (uses local fixtures — no network, no ffmpeg)

## Scaling past the 3 seed languages
Add entries to `languages.json` (metadata + hand-curated `forbidden`) and re-run.
No code change. Target: ~30–35 languages, growing toward the 102 FLEURS set.

## Where outputs go next
- `game.db` + `clips/` → Unity `Assets/StreamingAssets/` (runtime ClipService)
- `forbidden/*.json` → the oracle relay (leak filter + fact sheets)

## Known risk
FLEURS parquet audio field shape is confirmed in `prep/slice.py` (reads
`struct_extract(audio,'bytes')`). If a live pull raises "missing expected FLEURS
columns", inspect the real schema with:
`duckdb -c "DESCRIBE SELECT * FROM read_parquet('<parquet-url>')"` and adjust `slice.py`.
