"""
Efficient FLEURS fetcher -- ~70x less transfer than streaming via `datasets`.

Technique (verified empirically): the google/fleurs repo's ORIGINAL layout stores audio as
per-language tar.gz archives of individual .wav files (data/{lang}/audio/train.tar.gz) plus
a tiny train.tsv with all metadata. Because tar members are sequential, we can stream the
archive, decompress on the fly, and CLOSE THE CONNECTION after the first CLIPS_PER_LANG
wav files -- measured at ~10 MB / ~9 s per language instead of the ~700 MB+ / 5-15 min the
parquet route costs (its single giant row group forces readers to fetch the whole audio
column chunk; pyarrow, DuckDB, and the datasets-server /rows API all hit this wall).

Metadata comes from train.tsv (tab-separated: id, filename, raw_transcription,
transcription, char_segmentation, num_samples, gender), joined to each wav by filename.

Resumable: languages already complete in out/fleurs_metadata.csv are skipped.
"""
import io
import json
import pathlib
import tarfile
import time

import pandas as pd
import requests
import soundfile as sf
from huggingface_hub import get_token

CLIPS_PER_LANG = 15
REQUEST_TIMEOUT = 60          # per-read network timeout (connection-level, not whole-file)
LANG_RETRIES = 3

HERE = pathlib.Path(__file__).parent
OUT_DIR = HERE / "out"
CLIPS_DIR = OUT_DIR / "clips"
CLIPS_DIR.mkdir(parents=True, exist_ok=True)
CSV_PATH = OUT_DIR / "fleurs_metadata.csv"

BASE = "https://huggingface.co/datasets/google/fleurs/resolve/main/data"

languages = json.load(open(HERE.parent / "prep" / "languages.json"))["languages"]

rows = []
already_done = set()
if CSV_PATH.exists() and CSV_PATH.stat().st_size > 0:
    try:
        existing = pd.read_csv(CSV_PATH)
    except pd.errors.EmptyDataError:
        existing = pd.DataFrame()
    if not existing.empty:
        rows = existing.to_dict("records")
        counts = existing["lang_id"].value_counts()
        already_done = set(counts[counts >= CLIPS_PER_LANG].index)
        print(f"Resuming: {len(already_done)} language(s) already complete, skipping them.", flush=True)

session = requests.Session()
token = get_token()
if token:
    session.headers["Authorization"] = f"Bearer {token}"


class _StreamRaw(io.RawIOBase):
    """Adapts a requests streaming response into a raw file object for tarfile."""
    def __init__(self, resp):
        self._it = resp.iter_content(chunk_size=256 * 1024)
        self._buf = b""

    def readable(self):
        return True

    def readinto(self, b):
        while len(self._buf) < len(b):
            try:
                self._buf += next(self._it)
            except StopIteration:
                break
        n = min(len(b), len(self._buf))
        b[:n] = self._buf[:n]
        self._buf = self._buf[n:]
        return n


def fetch_tsv_metadata(lang_id):
    """Return {filename: row-dict} from the language's train.tsv (small file)."""
    resp = session.get(f"{BASE}/{lang_id}/train.tsv", timeout=REQUEST_TIMEOUT)
    resp.raise_for_status()
    meta = {}
    for line in resp.text.splitlines():
        parts = line.split("\t")
        if len(parts) < 7:
            continue
        meta[parts[1]] = {
            "id": parts[0],
            "raw_transcription": parts[2],
            "transcription": parts[3],
            "num_samples": parts[5],
            "gender": parts[6],
        }
    return meta


def pull_language(lang):
    lang_id = lang["lang_id"]
    tsv_meta = fetch_tsv_metadata(lang_id)

    resp = session.get(f"{BASE}/{lang_id}/audio/train.tar.gz", stream=True, timeout=REQUEST_TIMEOUT)
    resp.raise_for_status()
    tf = tarfile.open(fileobj=io.BufferedReader(_StreamRaw(resp)), mode="r|gz")

    lang_rows = []
    try:
        for member in tf:
            if not member.name.endswith(".wav"):
                continue
            wav_bytes = tf.extractfile(member).read()
            basename = member.name.rsplit("/", 1)[-1]

            samples, sample_rate = sf.read(io.BytesIO(wav_bytes))
            out_path = CLIPS_DIR / f"{lang_id}_{len(lang_rows):02d}.wav"
            sf.write(out_path, samples, sample_rate)

            m = tsv_meta.get(basename, {})
            lang_rows.append({
                "lang_id": lang_id,
                "language": lang["language"],
                "country": lang["country"],
                "continent": lang["continent"],
                "difficulty": lang["difficulty"],
                "id": m.get("id"),
                "num_samples": m.get("num_samples"),
                "gender": m.get("gender"),
                "transcription": m.get("transcription"),
                "raw_transcription": m.get("raw_transcription"),
                "sample_rate": sample_rate,
                "source_file": basename,
                "file_path": str(out_path),
            })
            if len(lang_rows) >= CLIPS_PER_LANG:
                break
    finally:
        resp.close()  # hard-stop the download; the rest of the archive is never fetched

    return lang_rows


if __name__ == "__main__":
    todo = [l for l in languages if l["lang_id"] not in already_done]
    failed = []
    print(f"Fetching {len(todo)} language(s), {CLIPS_PER_LANG} clips each.", flush=True)

    for k, lang in enumerate(todo, start=1):
        lang_id = lang["lang_id"]
        t0 = time.time()
        for attempt in range(1, LANG_RETRIES + 1):
            try:
                lang_rows = pull_language(lang)
                rows.extend(lang_rows)
                print(f"[{k}/{len(todo)}] {lang_id}: pulled {len(lang_rows)} clips in {time.time()-t0:.1f}s", flush=True)
                break
            except Exception as e:
                if attempt == LANG_RETRIES:
                    failed.append(lang_id)
                    print(f"[{k}/{len(todo)}] {lang_id}: FAILED after {LANG_RETRIES} attempts ({e!r})", flush=True)
                else:
                    print(f"[{k}/{len(todo)}] {lang_id}: attempt {attempt} failed ({e!r}), retrying...", flush=True)
        # Checkpoint after every language.
        pd.DataFrame(rows).to_csv(CSV_PATH, index=False)

    if failed:
        print(f"\n{len(failed)} language(s) failed: {failed}")
    else:
        print("\nAll languages pulled successfully.")
