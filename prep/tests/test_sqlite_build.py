import sqlite3
from prep.sqlite_build import create_schema, insert_clips, build_clip_rows, ClipRow
from prep.metadata import LanguageMeta


def test_build_rows_joins_metadata():
    meta = LanguageMeta("es_419", "Spanish", "Mexico", "North America", "common", ["Spanish"])
    rows = build_clip_rows(meta, [("clips/es_419_00000.ogg", "hola"), ("clips/es_419_00001.ogg", "adios")])
    assert len(rows) == 2
    assert rows[0].file == "clips/es_419_00000.ogg"
    assert rows[0].country == "Mexico"
    assert rows[0].difficulty == "common"
    assert rows[1].transcription == "adios"


def test_schema_and_insert_roundtrip():
    conn = sqlite3.connect(":memory:")
    create_schema(conn)
    rows = [ClipRow("clips/ja_jp_00000.ogg", "ja_jp", "Japanese", "Japan", "Asia", "konnichiwa", "common")]
    insert_clips(conn, rows)
    cur = conn.execute("SELECT file, language, country, continent, difficulty FROM clips")
    got = cur.fetchall()
    assert got == [("clips/ja_jp_00000.ogg", "Japanese", "Japan", "Asia", "common")]
    # id auto-assigned
    assert conn.execute("SELECT id FROM clips").fetchone()[0] == 1
    conn.close()
