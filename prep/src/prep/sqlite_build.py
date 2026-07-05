import sqlite3
from dataclasses import dataclass
from prep.metadata import LanguageMeta


@dataclass
class ClipRow:
    file: str
    lang_id: str
    language: str
    country: str
    continent: str
    transcription: str
    difficulty: str


_SCHEMA = """
CREATE TABLE clips (
  id            INTEGER PRIMARY KEY,
  file          TEXT NOT NULL,
  lang_id       TEXT NOT NULL,
  language      TEXT NOT NULL,
  country       TEXT NOT NULL,
  continent     TEXT NOT NULL,
  transcription TEXT,
  difficulty    TEXT
);
"""


def create_schema(conn: sqlite3.Connection) -> None:
    conn.execute("DROP TABLE IF EXISTS clips")
    conn.executescript(_SCHEMA)
    conn.commit()


def build_clip_rows(meta: LanguageMeta, files_and_transcriptions: list[tuple[str, str]]) -> list[ClipRow]:
    return [
        ClipRow(
            file=file,
            lang_id=meta.lang_id,
            language=meta.language,
            country=meta.country,
            continent=meta.continent,
            transcription=transcription,
            difficulty=meta.difficulty,
        )
        for file, transcription in files_and_transcriptions
    ]


def insert_clips(conn: sqlite3.Connection, rows: list[ClipRow]) -> None:
    conn.executemany(
        "INSERT INTO clips (file, lang_id, language, country, continent, transcription, difficulty) "
        "VALUES (?, ?, ?, ?, ?, ?, ?)",
        [(r.file, r.lang_id, r.language, r.country, r.continent, r.transcription, r.difficulty) for r in rows],
    )
    conn.commit()
