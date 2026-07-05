# Data Prep Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the offline, build-time-only pipeline that turns a hand-curated language metadata map + Google FLEURS audio into the game's shipped data: a folder of Ogg Vorbis clips, an embedded SQLite `clips` DB, and per-language `forbidden` lists for the oracle's leak filter.

**Architecture:** A small Python package (`prep`) of focused stages, each a pure-ish function with an injected dependency where it touches the outside world. `languages.json` is the hand-authored source of truth (metadata + forbidden words). Stages: stream N clips per language from FLEURS → encode each decoded array to Ogg Vorbis → build the SQLite DB → emit normalized forbidden lists. An orchestrator (`run_prep.py`) runs all stages for the configured languages. Pure logic is unit-tested against injected fakes (a fake example stream, a synth tone, in-memory SQLite); the real network pull is a final manual smoke run on a 3-language subset.

**Tech Stack:** Python 3.11+, `datasets` (HuggingFace FLEURS streaming pull), `soundfile`/libsndfile (Ogg Vorbis encode + decode), `numpy`, Python stdlib `sqlite3` + `json`, `pytest`.

> **Execution notes (deviations found while building, 2026-07-04):** the plan originally specified DuckDB-over-remote-parquet + ffmpeg. Reality forced two swaps, both preserving the same stage boundaries and the Ogg-Vorbis / SQLite outputs:
> 1. **Slice: DuckDB remote parquet → `datasets` streaming (with `Audio(decode=False)`).** The `google/fleurs` parquet files are ~1.8 GB each with enormous row groups and no page index, so DuckDB's httpfs range-reads fail (`Snappy decompression failure`) and the datasets-server `/rows` API 500s (`scan size limit exceeded`). HuggingFace `datasets` streaming is the canonical working pull; it still downloads the first row group (~690 MB/language) before yielding, which is inherent to this dataset. Additionally, `datasets>=5` requires the heavy `torchcodec` package to *decode* audio — avoided by `cast_column("audio", Audio(decode=False))`, which yields raw encoded bytes that we decode locally with `soundfile`. `slice_clips(lang_id, limit, source=None)` takes an injectable example stream for testing and handles both the bytes shape and an already-decoded array shape.
> 2. **Encode: ffmpeg → `soundfile`/libsndfile.** The available ffmpeg 8.1 build has no `libvorbis` and its native `vorbis` encoder is broken (produces 0 bytes even on a clean sine). libsndfile encodes Ogg Vorbis reliably and needs no external binary. `encode_ogg(samples, sample_rate, out_path)` takes the decoded array from the stream.
> `parquet_urls.py` (parquet URL resolver) was removed as dead code. FLEURS audio field shape confirmed live: `audio :: STRUCT(bytes BLOB, path VARCHAR)`, decoded by `datasets` to `{array, sampling_rate}`.

## Global Constraints

- **Build-time only:** nothing here ships in the game. `datasets`/`soundfile` are prep tools, not runtime dependencies (brief §8, §19).
- **Audio format:** every emitted clip is **Ogg Vorbis** (`.ogg`). Never emit or ship WAV (brief §6).
- **SQLite schema (verbatim, brief §7):**
  ```sql
  CREATE TABLE clips (
    id            INTEGER PRIMARY KEY,
    file          TEXT NOT NULL,      -- "clips/es_419_00412.ogg"
    lang_id       TEXT NOT NULL,      -- FLEURS id, e.g. "es_419"
    language      TEXT NOT NULL,      -- "Spanish"
    country       TEXT NOT NULL,      -- origin (the answer)
    continent     TEXT NOT NULL,
    transcription TEXT,
    difficulty    TEXT                -- "common" | "all"
  );
  ```
- **`forbidden` list contents (brief §11 Layer 1):** language, country, capital, demonym, currency, and unique landmarks/proper nouns per language. Hand-curated in `languages.json`.
- **Forbidden normalization:** emitted lists are lowercased, accent-stripped, de-duplicated, whole-word tokens — matching how the relay's Layer-3 filter compares (case-insensitive, accent-insensitive, whole-word).
- **Initial scope:** wire and prove the pipeline on **3 languages** (`es_419` Spanish/Mexico, `ja_jp` Japanese/Japan, `sw_ke` Swahili/Kenya). Scaling to 30–35 is data entry into `languages.json`, no code change.
- **FLEURS shape risk (brief §18):** the audio field shape must be confirmed against a live parquet; the slice stage isolates that assumption in one place and fails loudly on undecodable clips.
- **Licensing (brief §17):** clips derive from FLEURS (CC-BY-4.0) over the FLoRes corpus — attribution is a game-side screen, out of scope here, but noted so no one strips provenance.
- **All paths in this plan are relative to `prep/`** at the project root.

**Setup precondition (one-time, not a test cycle):** Install Python 3.11+, `ffmpeg` on `PATH` (`brew install ffmpeg`), and create a virtualenv. Confirm with `python --version`, `ffmpeg -version`, and (after Task 1) `pytest --version`.

---

### Task 1: Package scaffold + `languages.json` seed + config

**Files:**
- Create: `prep/requirements.txt`
- Create: `prep/pytest.ini`
- Create: `prep/.gitignore`
- Create: `prep/src/prep/__init__.py`
- Create: `prep/languages.json`
- Test: `prep/tests/test_smoke.py`

**Interfaces:**
- Consumes: nothing (first task).
- Produces: an importable `prep` package and the hand-curated `languages.json` all later stages read.

- [ ] **Step 1: Create `prep/requirements.txt`**

```
duckdb==1.1.3
pytest==8.3.2
```

- [ ] **Step 2: Create `prep/pytest.ini`**

```ini
[pytest]
pythonpath = src
testpaths = tests
```

- [ ] **Step 3: Create `prep/.gitignore`**

```
__pycache__/
*.pyc
.venv/
out/
```

- [ ] **Step 4: Create `prep/src/prep/__init__.py`** (empty package marker)

```python
```

- [ ] **Step 5: Create `prep/languages.json`** (hand-curated seed — 3 languages)

```json
{
  "clips_per_language": 30,
  "languages": [
    {
      "lang_id": "es_419",
      "language": "Spanish",
      "country": "Mexico",
      "continent": "North America",
      "difficulty": "common",
      "forbidden": [
        "Spanish", "Espanol", "Español", "Castilian",
        "Mexico", "Mexican", "Mexicano", "Mexico City",
        "Peso", "Aztec", "Maya", "Mayan", "Spain", "Latin America"
      ]
    },
    {
      "lang_id": "ja_jp",
      "language": "Japanese",
      "country": "Japan",
      "continent": "Asia",
      "difficulty": "common",
      "forbidden": [
        "Japanese", "Nihongo", "Japan", "Nippon", "Nihon",
        "Tokyo", "Yen", "Kanji", "Hiragana", "Katakana",
        "Samurai", "Mount Fuji", "Fuji", "Kimono", "Sushi"
      ]
    },
    {
      "lang_id": "sw_ke",
      "language": "Swahili",
      "country": "Kenya",
      "continent": "Africa",
      "difficulty": "all",
      "forbidden": [
        "Swahili", "Kiswahili", "Kenya", "Kenyan",
        "Nairobi", "Shilling", "Maasai", "Masai",
        "Serengeti", "Kilimanjaro", "Safari"
      ]
    }
  ]
}
```

- [ ] **Step 6: Install dependencies**

Run: `cd prep && python -m venv .venv && . .venv/bin/activate && pip install -r requirements.txt`
Expected: duckdb + pytest installed, no error.

- [ ] **Step 7: Write the failing test** in `prep/tests/test_smoke.py`

```python
import json
from pathlib import Path


def test_languages_json_has_three_seed_languages():
    data = json.loads(Path("languages.json").read_text())
    ids = [lang["lang_id"] for lang in data["languages"]]
    assert ids == ["es_419", "ja_jp", "sw_ke"]


def test_prep_package_importable():
    import prep  # noqa: F401
```

- [ ] **Step 8: Run test to verify it passes**

Run: `cd prep && python -m pytest tests/test_smoke.py -v`
Expected: PASS (2 tests). (This task's deliverable is data + scaffold; the "test" confirms both exist and import.)

- [ ] **Step 9: Commit**

```bash
cd prep && git add requirements.txt pytest.ini .gitignore src/prep/__init__.py languages.json tests/test_smoke.py
git commit -m "chore(prep): scaffold Python package and seed languages.json"
```

---

### Task 2: Metadata loader + validator

**Files:**
- Create: `prep/src/prep/metadata.py`
- Test: `prep/tests/test_metadata.py`

**Interfaces:**
- Consumes: `languages.json`.
- Produces:
  - `@dataclass LanguageMeta` with fields `lang_id, language, country, continent, difficulty, forbidden: list[str]`.
  - `load_metadata(path: str) -> tuple[int, list[LanguageMeta]]` returning `(clips_per_language, languages)`.
  - `validate(languages: list[LanguageMeta]) -> None` — raises `ValueError` on: missing required field, empty `forbidden`, `difficulty` not in `{"common","all"}`, or duplicate `lang_id`.

- [ ] **Step 1: Write the failing test** in `prep/tests/test_metadata.py`

```python
import json
import pytest
from prep.metadata import load_metadata, validate, LanguageMeta


def write(tmp_path, obj):
    p = tmp_path / "languages.json"
    p.write_text(json.dumps(obj))
    return str(p)


BASE = {
    "clips_per_language": 5,
    "languages": [
        {"lang_id": "es_419", "language": "Spanish", "country": "Mexico",
         "continent": "North America", "difficulty": "common",
         "forbidden": ["Spanish", "Mexico"]},
    ],
}


def test_load_reads_count_and_languages(tmp_path):
    count, langs = load_metadata(write(tmp_path, BASE))
    assert count == 5
    assert len(langs) == 1
    assert langs[0].language == "Spanish"
    assert langs[0].forbidden == ["Spanish", "Mexico"]


def test_validate_rejects_empty_forbidden():
    langs = [LanguageMeta("x_xx", "X", "C", "Asia", "all", [])]
    with pytest.raises(ValueError, match="forbidden"):
        validate(langs)


def test_validate_rejects_bad_difficulty():
    langs = [LanguageMeta("x_xx", "X", "C", "Asia", "medium", ["a"])]
    with pytest.raises(ValueError, match="difficulty"):
        validate(langs)


def test_validate_rejects_duplicate_lang_id():
    langs = [
        LanguageMeta("x_xx", "X", "C", "Asia", "all", ["a"]),
        LanguageMeta("x_xx", "Y", "D", "Asia", "all", ["b"]),
    ]
    with pytest.raises(ValueError, match="duplicate"):
        validate(langs)
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd prep && python -m pytest tests/test_metadata.py -v`
Expected: FAIL — `prep.metadata` does not exist.

- [ ] **Step 3: Write `prep/src/prep/metadata.py`**

```python
import json
from dataclasses import dataclass


@dataclass
class LanguageMeta:
    lang_id: str
    language: str
    country: str
    continent: str
    difficulty: str
    forbidden: list[str]


_REQUIRED = ("lang_id", "language", "country", "continent", "difficulty")


def load_metadata(path: str) -> tuple[int, list[LanguageMeta]]:
    data = json.loads(open(path, encoding="utf-8").read())
    count = int(data["clips_per_language"])
    langs = [
        LanguageMeta(
            lang_id=item["lang_id"],
            language=item["language"],
            country=item["country"],
            continent=item["continent"],
            difficulty=item["difficulty"],
            forbidden=list(item["forbidden"]),
        )
        for item in data["languages"]
    ]
    return count, langs


def validate(languages: list[LanguageMeta]) -> None:
    seen: set[str] = set()
    for lang in languages:
        for field in _REQUIRED:
            if not getattr(lang, field):
                raise ValueError(f"{lang.lang_id}: missing required field '{field}'")
        if lang.difficulty not in ("common", "all"):
            raise ValueError(f"{lang.lang_id}: difficulty must be 'common' or 'all'")
        if not lang.forbidden:
            raise ValueError(f"{lang.lang_id}: forbidden list must not be empty")
        if lang.lang_id in seen:
            raise ValueError(f"duplicate lang_id: {lang.lang_id}")
        seen.add(lang.lang_id)
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd prep && python -m pytest tests/test_metadata.py -v`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
cd prep && git add src/prep/metadata.py tests/test_metadata.py
git commit -m "feat(prep): metadata loader and validator"
```

---

### Task 3: FLEURS parquet URL resolver

**Files:**
- Create: `prep/src/prep/parquet_urls.py`
- Create: `prep/tests/fixtures/parquet_listing.json`
- Test: `prep/tests/test_parquet_urls.py`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `parse_parquet_urls(listing: dict, split: str = "train") -> list[str]` — pure: extracts `.url` for rows matching `split` from a HuggingFace datasets-server `/parquet` response.
  - `resolve_parquet_urls(lang_id: str, fetch=urlopen_json) -> list[str]` — calls the datasets-server `/parquet?dataset=google/fleurs&config=<lang_id>` endpoint (via injected `fetch`) and returns train-split URLs.

**Note:** the datasets-server `/parquet` response shape is `{"parquet_files": [{"config","split","url",...}, ...]}`. The pure parser is unit-tested against a saved fixture; the live HTTP call is exercised only in the Task 8 smoke run.

- [ ] **Step 1: Create the fixture** `prep/tests/fixtures/parquet_listing.json`

```json
{
  "parquet_files": [
    { "dataset": "google/fleurs", "config": "es_419", "split": "train",
      "url": "https://huggingface.co/datasets/google/fleurs/resolve/refs%2Fconvert%2Fparquet/es_419/train/0000.parquet" },
    { "dataset": "google/fleurs", "config": "es_419", "split": "test",
      "url": "https://huggingface.co/datasets/google/fleurs/resolve/refs%2Fconvert%2Fparquet/es_419/test/0000.parquet" }
  ]
}
```

- [ ] **Step 2: Write the failing test** in `prep/tests/test_parquet_urls.py`

```python
import json
from pathlib import Path
from prep.parquet_urls import parse_parquet_urls, resolve_parquet_urls

LISTING = json.loads(Path("tests/fixtures/parquet_listing.json").read_text())


def test_parse_returns_only_train_urls():
    urls = parse_parquet_urls(LISTING, split="train")
    assert len(urls) == 1
    assert urls[0].endswith("es_419/train/0000.parquet")


def test_resolve_uses_injected_fetch():
    calls = []

    def fake_fetch(url):
        calls.append(url)
        return LISTING

    urls = resolve_parquet_urls("es_419", fetch=fake_fetch)
    assert urls[0].endswith("train/0000.parquet")
    assert "config=es_419" in calls[0]
    assert "dataset=google/fleurs" in calls[0]
```

- [ ] **Step 3: Run test to verify it fails**

Run: `cd prep && python -m pytest tests/test_parquet_urls.py -v`
Expected: FAIL — `prep.parquet_urls` does not exist.

- [ ] **Step 4: Write `prep/src/prep/parquet_urls.py`**

```python
import json
from urllib.request import urlopen
from urllib.parse import urlencode

_ENDPOINT = "https://datasets-server.huggingface.co/parquet"


def urlopen_json(url: str) -> dict:
    with urlopen(url) as resp:
        return json.loads(resp.read().decode("utf-8"))


def parse_parquet_urls(listing: dict, split: str = "train") -> list[str]:
    return [
        f["url"]
        for f in listing.get("parquet_files", [])
        if f.get("split") == split
    ]


def resolve_parquet_urls(lang_id: str, fetch=urlopen_json) -> list[str]:
    query = urlencode({"dataset": "google/fleurs", "config": lang_id})
    listing = fetch(f"{_ENDPOINT}?{query}")
    return parse_parquet_urls(listing, split="train")
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd prep && python -m pytest tests/test_parquet_urls.py -v`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
cd prep && git add src/prep/parquet_urls.py tests/fixtures/parquet_listing.json tests/test_parquet_urls.py
git commit -m "feat(prep): FLEURS parquet URL resolver"
```

---

### Task 4: DuckDB clip slicer

**Files:**
- Create: `prep/src/prep/slice.py`
- Test: `prep/tests/test_slice.py`

**Interfaces:**
- Consumes: nothing (given parquet URLs/paths).
- Produces:
  - `@dataclass RawClip` with `audio_bytes: bytes`, `transcription: str`.
  - `slice_clips(parquet_source: str, limit: int, con=None) -> list[RawClip]` — runs a DuckDB query selecting the FLEURS `audio.bytes` + `transcription` from the parquet, `LIMIT limit`, returning decoded rows. Raises `RuntimeError` with a clear message if the expected `audio`/`transcription` columns are absent (the FLEURS-shape guard).
  - `make_fixture_parquet(path: str, rows: list[tuple[bytes, str]]) -> None` — test helper that writes a parquet with FLEURS-shaped columns (`audio STRUCT(bytes BLOB, path VARCHAR)`, `transcription VARCHAR`).

**Note:** FLEURS stores audio as a struct with a `bytes` field (the encoded WAV). `slice_clips` reads `struct_extract(audio, 'bytes')`. The struct-shape assumption lives ONLY here (brief §18 risk isolation).

- [ ] **Step 1: Write the failing test** in `prep/tests/test_slice.py`

```python
import pytest
from prep.slice import slice_clips, make_fixture_parquet, RawClip


def test_slice_reads_audio_bytes_and_transcription(tmp_path):
    parquet = str(tmp_path / "f.parquet")
    make_fixture_parquet(parquet, [
        (b"RIFFfake1", "hola mundo"),
        (b"RIFFfake2", "buenos dias"),
        (b"RIFFfake3", "adios"),
    ])
    clips = slice_clips(parquet, limit=2)
    assert len(clips) == 2
    assert isinstance(clips[0], RawClip)
    assert clips[0].audio_bytes == b"RIFFfake1"
    assert clips[0].transcription == "hola mundo"


def test_slice_raises_on_missing_columns(tmp_path):
    import duckdb
    parquet = str(tmp_path / "bad.parquet")
    con = duckdb.connect()
    con.execute("CREATE TABLE t AS SELECT 1 AS wrong")
    con.execute(f"COPY t TO '{parquet}' (FORMAT PARQUET)")
    con.close()
    with pytest.raises(RuntimeError, match="audio"):
        slice_clips(parquet, limit=1)
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd prep && python -m pytest tests/test_slice.py -v`
Expected: FAIL — `prep.slice` does not exist.

- [ ] **Step 3: Write `prep/src/prep/slice.py`**

```python
from dataclasses import dataclass
import duckdb


@dataclass
class RawClip:
    audio_bytes: bytes
    transcription: str


def make_fixture_parquet(path: str, rows: list[tuple[bytes, str]]) -> None:
    con = duckdb.connect()
    con.execute(
        "CREATE TABLE t (audio STRUCT(bytes BLOB, \"path\" VARCHAR), transcription VARCHAR)"
    )
    for audio_bytes, transcription in rows:
        con.execute(
            "INSERT INTO t VALUES ({'bytes': ?::BLOB, 'path': 'x.wav'}, ?)",
            [audio_bytes, transcription],
        )
    con.execute(f"COPY t TO '{path}' (FORMAT PARQUET)")
    con.close()


def slice_clips(parquet_source: str, limit: int, con=None) -> list[RawClip]:
    owns = con is None
    con = con or duckdb.connect()
    try:
        con.execute("INSTALL httpfs; LOAD httpfs;") if parquet_source.startswith("http") else None
        cols = {
            r[0]
            for r in con.execute(
                "SELECT column_name FROM (DESCRIBE SELECT * FROM read_parquet(?))",
                [parquet_source],
            ).fetchall()
        }
        if "audio" not in cols or "transcription" not in cols:
            raise RuntimeError(
                f"parquet missing expected FLEURS columns (audio, transcription); got {sorted(cols)}"
            )
        rows = con.execute(
            "SELECT struct_extract(audio, 'bytes') AS audio_bytes, transcription "
            "FROM read_parquet(?) LIMIT ?",
            [parquet_source, limit],
        ).fetchall()
        return [RawClip(audio_bytes=bytes(r[0]), transcription=r[1]) for r in rows]
    finally:
        if owns:
            con.close()
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd prep && python -m pytest tests/test_slice.py -v`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
cd prep && git add src/prep/slice.py tests/test_slice.py
git commit -m "feat(prep): DuckDB FLEURS clip slicer with shape guard"
```

---

### Task 5: Ogg Vorbis encoder (ffmpeg)

**Files:**
- Create: `prep/src/prep/encode.py`
- Test: `prep/tests/test_encode.py`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `encode_ogg(audio_bytes: bytes, out_path: str) -> None` — writes the input audio bytes to a temp file, invokes `ffmpeg` to transcode to Ogg Vorbis at `out_path`, raises `RuntimeError` if ffmpeg fails.
  - `clip_filename(lang_id: str, index: int) -> str` — returns `"clips/{lang_id}_{index:05d}.ogg"`.

**Note:** this is an integration test — it shells out to the real `ffmpeg` (a documented prereq) and generates a genuine tiny WAV in-test so the encode is real, not mocked.

- [ ] **Step 1: Write the failing test** in `prep/tests/test_encode.py`

```python
import struct
import subprocess
from pathlib import Path
import pytest
from prep.encode import encode_ogg, clip_filename


def tiny_wav_bytes() -> bytes:
    # 8-bit mono, 8000 Hz, 100 samples of silence — a minimal valid WAV.
    sample_rate = 8000
    data = b"\x80" * 100
    header = b"RIFF" + struct.pack("<I", 36 + len(data)) + b"WAVE"
    header += b"fmt " + struct.pack("<IHHIIHH", 16, 1, 1, sample_rate, sample_rate, 1, 8)
    header += b"data" + struct.pack("<I", len(data))
    return header + data


def test_clip_filename_zero_pads():
    assert clip_filename("es_419", 12) == "clips/es_419_00012.ogg"


def test_encode_produces_a_valid_ogg(tmp_path):
    out = tmp_path / "clip.ogg"
    encode_ogg(tiny_wav_bytes(), str(out))
    assert out.exists() and out.stat().st_size > 0
    # ffprobe confirms the container really is ogg/vorbis
    probe = subprocess.run(
        ["ffprobe", "-v", "error", "-show_entries", "format=format_name",
         "-of", "default=nw=1:nk=1", str(out)],
        capture_output=True, text=True,
    )
    assert "ogg" in probe.stdout


def test_encode_raises_on_garbage_input(tmp_path):
    with pytest.raises(RuntimeError):
        encode_ogg(b"not audio at all", str(tmp_path / "bad.ogg"))
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd prep && python -m pytest tests/test_encode.py -v`
Expected: FAIL — `prep.encode` does not exist.

- [ ] **Step 3: Write `prep/src/prep/encode.py`**

```python
import subprocess
import tempfile
from pathlib import Path


def clip_filename(lang_id: str, index: int) -> str:
    return f"clips/{lang_id}_{index:05d}.ogg"


def encode_ogg(audio_bytes: bytes, out_path: str) -> None:
    Path(out_path).parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(suffix=".input", delete=True) as src:
        src.write(audio_bytes)
        src.flush()
        result = subprocess.run(
            ["ffmpeg", "-y", "-i", src.name, "-c:a", "libvorbis", "-q:a", "4", out_path],
            capture_output=True, text=True,
        )
    if result.returncode != 0:
        raise RuntimeError(f"ffmpeg failed ({result.returncode}): {result.stderr[-400:]}")
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd prep && python -m pytest tests/test_encode.py -v`
Expected: PASS (3 tests). (Requires `ffmpeg`/`ffprobe` on PATH.)

- [ ] **Step 5: Commit**

```bash
cd prep && git add src/prep/encode.py tests/test_encode.py
git commit -m "feat(prep): ffmpeg Ogg Vorbis encoder"
```

---

### Task 6: SQLite DB builder

**Files:**
- Create: `prep/src/prep/sqlite_build.py`
- Test: `prep/tests/test_sqlite_build.py`

**Interfaces:**
- Consumes: `LanguageMeta` from `metadata.py`.
- Produces:
  - `@dataclass ClipRow` with `file, lang_id, language, country, continent, transcription, difficulty`.
  - `create_schema(conn) -> None` — creates the `clips` table (schema verbatim from Global Constraints).
  - `insert_clips(conn, rows: list[ClipRow]) -> None` — inserts rows (auto `id`).
  - `build_clip_rows(meta: LanguageMeta, files_and_transcriptions: list[tuple[str, str]]) -> list[ClipRow]` — joins per-clip `(file, transcription)` with the language's metadata.

- [ ] **Step 1: Write the failing test** in `prep/tests/test_sqlite_build.py`

```python
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
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd prep && python -m pytest tests/test_sqlite_build.py -v`
Expected: FAIL — `prep.sqlite_build` does not exist.

- [ ] **Step 3: Write `prep/src/prep/sqlite_build.py`**

```python
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd prep && python -m pytest tests/test_sqlite_build.py -v`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
cd prep && git add src/prep/sqlite_build.py tests/test_sqlite_build.py
git commit -m "feat(prep): SQLite clips DB builder"
```

---

### Task 7: Forbidden-list normalizer + emitter

**Files:**
- Create: `prep/src/prep/forbidden.py`
- Test: `prep/tests/test_forbidden.py`

**Interfaces:**
- Consumes: `LanguageMeta`.
- Produces:
  - `normalize_forbidden(words: list[str]) -> list[str]` — lowercases, strips accents (NFKD), trims, drops empties, de-duplicates while preserving first-seen order.
  - `write_forbidden_files(languages: list[LanguageMeta], out_dir: str) -> None` — writes `out_dir/{lang_id}.json` as `{ "lang_id", "language", "country", "continent", "forbidden": [normalized...] }` (the relay/host fact sheet, brief §11 Layer 1).

- [ ] **Step 1: Write the failing test** in `prep/tests/test_forbidden.py`

```python
import json
from pathlib import Path
from prep.forbidden import normalize_forbidden, write_forbidden_files
from prep.metadata import LanguageMeta


def test_normalize_lowercases_strips_accents_dedupes():
    out = normalize_forbidden(["Español", "espanol", "  Mexico  ", "", "MEXICO"])
    assert out == ["espanol", "mexico"]


def test_write_forbidden_files_emits_fact_sheet(tmp_path):
    langs = [LanguageMeta("es_419", "Spanish", "Mexico", "North America", "common", ["Spanish", "México"])]
    write_forbidden_files(langs, str(tmp_path))
    sheet = json.loads((tmp_path / "es_419.json").read_text())
    assert sheet["language"] == "Spanish"
    assert sheet["country"] == "Mexico"
    assert "spanish" in sheet["forbidden"]
    assert "mexico" in sheet["forbidden"]
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd prep && python -m pytest tests/test_forbidden.py -v`
Expected: FAIL — `prep.forbidden` does not exist.

- [ ] **Step 3: Write `prep/src/prep/forbidden.py`**

```python
import json
import unicodedata
from pathlib import Path
from prep.metadata import LanguageMeta


def _strip_accents(text: str) -> str:
    decomposed = unicodedata.normalize("NFKD", text)
    return "".join(c for c in decomposed if not unicodedata.combining(c))


def normalize_forbidden(words: list[str]) -> list[str]:
    result: list[str] = []
    seen: set[str] = set()
    for word in words:
        norm = _strip_accents(word).lower().strip()
        if norm and norm not in seen:
            seen.add(norm)
            result.append(norm)
    return result


def write_forbidden_files(languages: list[LanguageMeta], out_dir: str) -> None:
    Path(out_dir).mkdir(parents=True, exist_ok=True)
    for lang in languages:
        sheet = {
            "lang_id": lang.lang_id,
            "language": lang.language,
            "country": lang.country,
            "continent": lang.continent,
            "forbidden": normalize_forbidden(lang.forbidden),
        }
        (Path(out_dir) / f"{lang.lang_id}.json").write_text(
            json.dumps(sheet, ensure_ascii=False, indent=2), encoding="utf-8"
        )
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd prep && python -m pytest tests/test_forbidden.py -v`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
cd prep && git add src/prep/forbidden.py tests/test_forbidden.py
git commit -m "feat(prep): forbidden-list normalizer and fact-sheet emitter"
```

---

### Task 8: Orchestrator + real 3-language smoke run

**Files:**
- Create: `prep/src/prep/run_prep.py`
- Create: `prep/README.md`
- Test: `prep/tests/test_run_prep.py`

**Interfaces:**
- Consumes: every stage above.
- Produces:
  - `run(languages_json: str, out_dir: str, resolver=resolve_parquet_urls, slicer=slice_clips, encoder=encode_ogg) -> dict` — orchestrates: load+validate metadata → for each language resolve parquet, slice `clips_per_language`, encode each to `out_dir/clips/...`, collect `(file, transcription)` → build `out_dir/game.db` → write `out_dir/forbidden/`. Returns a summary `{lang_id: clip_count}`. Dependencies are injected so the orchestration is unit-testable with fakes.
  - `python -m prep.run_prep` CLI entry (defaults `languages.json` → `out/`).

- [ ] **Step 1: Write the failing test** in `prep/tests/test_run_prep.py` (fakes — no network/ffmpeg)

```python
import sqlite3
from pathlib import Path
from prep.run_prep import run
from prep.slice import RawClip


def fake_resolver(lang_id):
    return [f"fake://{lang_id}.parquet"]


def fake_slicer(parquet_source, limit, con=None):
    return [RawClip(audio_bytes=b"AUDIO", transcription=f"line{i}") for i in range(limit)]


def fake_encoder(audio_bytes, out_path):
    Path(out_path).parent.mkdir(parents=True, exist_ok=True)
    Path(out_path).write_bytes(b"OGG")


def test_run_builds_db_clips_and_forbidden(tmp_path):
    out = str(tmp_path / "out")
    summary = run("languages.json", out, resolver=fake_resolver, slicer=fake_slicer, encoder=fake_encoder)

    # 3 seed languages, 30 clips each
    assert summary == {"es_419": 30, "ja_jp": 30, "sw_ke": 30}

    # clips written
    assert (Path(out) / "clips" / "es_419_00000.ogg").exists()

    # db has 90 rows with correct join
    conn = sqlite3.connect(str(Path(out) / "game.db"))
    assert conn.execute("SELECT COUNT(*) FROM clips").fetchone()[0] == 90
    row = conn.execute("SELECT country FROM clips WHERE lang_id='ja_jp' LIMIT 1").fetchone()
    assert row[0] == "Japan"
    conn.close()

    # forbidden fact sheets written + normalized
    import json
    sheet = json.loads((Path(out) / "forbidden" / "sw_ke.json").read_text())
    assert "kenya" in sheet["forbidden"]
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd prep && python -m pytest tests/test_run_prep.py -v`
Expected: FAIL — `prep.run_prep` does not exist.

- [ ] **Step 3: Write `prep/src/prep/run_prep.py`**

```python
import sqlite3
import sys
from pathlib import Path

from prep.metadata import load_metadata, validate
from prep.parquet_urls import resolve_parquet_urls
from prep.slice import slice_clips
from prep.encode import encode_ogg, clip_filename
from prep.sqlite_build import create_schema, insert_clips, build_clip_rows
from prep.forbidden import write_forbidden_files


def run(languages_json, out_dir, resolver=resolve_parquet_urls, slicer=slice_clips, encoder=encode_ogg):
    clips_per_language, languages = load_metadata(languages_json)
    validate(languages)

    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)

    conn = sqlite3.connect(str(out / "game.db"))
    create_schema(conn)

    summary = {}
    for lang in languages:
        parquet_urls = resolver(lang.lang_id)
        raw = slicer(parquet_urls[0], limit=clips_per_language)
        files_and_transcriptions = []
        for index, clip in enumerate(raw):
            rel = clip_filename(lang.lang_id, index)
            encoder(clip.audio_bytes, str(out / rel))
            files_and_transcriptions.append((rel, clip.transcription))
        rows = build_clip_rows(lang, files_and_transcriptions)
        insert_clips(conn, rows)
        summary[lang.lang_id] = len(rows)

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
    for lang_id, count in summary.items():
        print(f"  {lang_id}: {count}")


if __name__ == "__main__":
    main()
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd prep && python -m pytest tests/test_run_prep.py -v`
Expected: PASS (1 test).

- [ ] **Step 5: Run the FULL unit suite**

Run: `cd prep && python -m pytest -v`
Expected: PASS — every test file green (smoke, metadata, parquet_urls, slice, encode, sqlite_build, forbidden, run_prep).

- [ ] **Step 6: Write `prep/README.md`**

```markdown
# Say Again? — Data Prep Pipeline (offline, build-time only)

Turns `languages.json` (hand-curated metadata + forbidden words) + Google FLEURS
audio into the game's shipped data. NOT shipped in the game; DuckDB & ffmpeg are
prep tools only.

## Prereqs
- Python 3.11+, `ffmpeg` + `ffprobe` on PATH (`brew install ffmpeg`)
- `python -m venv .venv && . .venv/bin/activate && pip install -r requirements.txt`

## Run (real FLEURS pull)
- `python -m prep.run_prep languages.json out`
- Outputs into `out/`:
  - `clips/` — Ogg Vorbis clips (`<lang_id>_<n>.ogg`)
  - `game.db` — SQLite `clips` table (ship in Unity StreamingAssets)
  - `forbidden/<lang_id>.json` — per-language fact sheets for the oracle relay

## Test
- `python -m pytest` (uses local fixtures — no network, but needs ffmpeg for encode tests)

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
```

- [ ] **Step 7: Real smoke run (the integration gate for the whole pipeline)**

Run: `cd prep && . .venv/bin/activate && python -m prep.run_prep languages.json out`
Expected: prints `Prepared 90 clips across 3 languages` (or the configured count). Then verify:
- `ls out/clips | head` shows real `.ogg` files.
- `ffprobe out/clips/es_419_00000.ogg` reports ogg/vorbis and a nonzero duration.
- `duckdb :memory: "SELECT lang_id, COUNT(*) FROM read_parquet('/dev/null')"` not needed — instead: `sqlite3 out/game.db "SELECT lang_id, COUNT(*) FROM clips GROUP BY lang_id"` shows 3 languages.
- `cat out/forbidden/ja_jp.json` shows a normalized lowercase forbidden list.

If the run raises "missing expected FLEURS columns", follow the README "Known risk" step to inspect the live schema and adjust `slice.py`, then re-run. This is the brief §18 FLEURS-shape confirmation.

- [ ] **Step 8: Commit**

```bash
cd prep && git add src/prep/run_prep.py README.md tests/test_run_prep.py
git commit -m "feat(prep): orchestrator, CLI, and docs; end-to-end pipeline"
```

---

## Task 9 (note, not a code cycle): scale to 30–35 languages

Not part of this plan's test cycle — a follow-up data task once the 3-language pipeline is proven:
- Add ~30 more entries to `languages.json` spread across all continents, each with hand-curated `forbidden` (language, country, capital, demonym, currency, landmarks/proper nouns).
- Re-run `python -m prep.run_prep languages.json out`.
- Spot-check a sample of `forbidden` lists for missed giveaways (a missed word is a leak).
- Copy `out/game.db` + `out/clips/` into Unity `StreamingAssets`; deliver `out/forbidden/` to the relay.

---

## Self-Review

- **Spec coverage:** offline FLEURS→Ogg→SQLite pipeline (brief §8, Tasks 3–8), SQLite schema verbatim (§7, Task 6), Ogg-only never WAV (§6, Task 5), hand-curated forbidden lists = Layer 1 fact sheets (§11, Tasks 1 & 7), DuckDB as prep-only slice tool (§8, Task 4), start-3-then-30-35 growth without code change (§8, Tasks 1 & 9), FLEURS-shape risk isolated + confirmed (§18, Tasks 4 & 8). Output routing to Unity StreamingAssets + relay documented (Task 8 README) for the downstream plans.
- **Placeholder scan:** the 3-language seed and Task 9 scale-up are intentional, documented data tasks, not vague TODOs. No "add error handling" hand-waving — each stage raises concrete, tested errors (`RuntimeError` on bad parquet/ffmpeg, `ValueError` on bad metadata).
- **Type consistency:** `LanguageMeta`, `RawClip`, `ClipRow`, `clip_filename`, `normalize_forbidden`, `slice_clips`, `resolve_parquet_urls`, and `encode_ogg` keep identical names/signatures across the tasks that define and consume them; `run()`'s injected `resolver`/`slicer`/`encoder` match the real functions' signatures so the fakes in Task 8 and the production defaults are interchangeable.
```
