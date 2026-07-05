import sqlite3
import sys
from pathlib import Path

from prep.metadata import load_metadata, validate
from prep.slice import slice_clips
from prep.encode import encode_ogg, clip_filename
from prep.sqlite_build import create_schema, insert_clips, build_clip_rows
from prep.forbidden import write_forbidden_files


def run(languages_json, out_dir, slicer=slice_clips, encoder=encode_ogg):
    clips_per_language, languages = load_metadata(languages_json)
    validate(languages)

    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)

    conn = sqlite3.connect(str(out / "game.db"))
    create_schema(conn)

    summary = {}
    for lang in languages:
        raw = slicer(lang.lang_id, limit=clips_per_language)
        files_and_transcriptions = []
        for index, clip in enumerate(raw):
            rel = clip_filename(lang.lang_id, index)
            encoder(clip.samples, clip.sample_rate, str(out / rel))
            files_and_transcriptions.append((rel, clip.transcription))
        rows = build_clip_rows(lang, files_and_transcriptions)
        insert_clips(conn, rows)
        summary[lang.lang_id] = len(rows)
        print(f"  {lang.lang_id}: {len(rows)} clips", flush=True)

    conn.close()
    write_forbidden_files(languages, str(out / "forbidden"))
    return summary


def main(argv=None):
    argv = argv or sys.argv[1:]
    languages_json = argv[0] if len(argv) > 0 else "languages.json"
    out_dir = argv[1] if len(argv) > 1 else "out"
    summary = run(languages_json, out_dir)
    total = sum(summary.values())
    print(f"Prepared {total} clips across {len(summary)} languages into {out_dir}/")


if __name__ == "__main__":
    main()
