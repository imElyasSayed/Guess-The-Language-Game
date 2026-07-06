import json
import sqlite3
from pathlib import Path
import numpy as np
from prep.run_prep import run
from prep.slice import RawClip


def fake_slicer(lang_id, limit):
    return [
        RawClip(samples=np.array([0.0, 0.1], dtype="float32"), sample_rate=16000,
                transcription=f"{lang_id} line{i}")
        for i in range(limit)
    ]


def fake_encoder(samples, sample_rate, out_path):
    Path(out_path).parent.mkdir(parents=True, exist_ok=True)
    Path(out_path).write_bytes(b"OGG")


def test_run_builds_db_clips_and_forbidden(tmp_path):
    out = str(tmp_path / "out")
    summary = run("languages.json", out, slicer=fake_slicer, encoder=fake_encoder)

    languages = json.loads(Path("languages.json").read_text())["languages"]
    clips_per_language = json.loads(Path("languages.json").read_text())["clips_per_language"]

    # every language in languages.json gets clips_per_language clips
    assert summary == {lang["lang_id"]: clips_per_language for lang in languages}

    # clips written
    assert (Path(out) / "clips" / "es_419_00000.ogg").exists()

    # db has one row per language x clips_per_language, with correct join
    conn = sqlite3.connect(str(Path(out) / "game.db"))
    assert conn.execute("SELECT COUNT(*) FROM clips").fetchone()[0] == len(languages) * clips_per_language
    row = conn.execute("SELECT country FROM clips WHERE lang_id='ja_jp' LIMIT 1").fetchone()
    assert row[0] == "Japan"
    conn.close()

    # forbidden fact sheets written + normalized
    sheet = json.loads((Path(out) / "forbidden" / "sw_ke.json").read_text())
    assert "kenya" in sheet["forbidden"]
