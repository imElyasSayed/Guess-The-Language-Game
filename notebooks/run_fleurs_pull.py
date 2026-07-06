"""
Standalone headless runner for the bulk FLEURS pull (same logic as fleurs_explore.ipynb's
pull cell). Runs as a plain script -- not through a Jupyter kernel -- so heavy concurrent
network I/O across MAX_CONCURRENT languages can't trip a ZMQ heartbeat timeout and cause a
false "kernel died" the way `jupyter nbconvert --execute` did.
"""
import json
import random
import sys
import pathlib
import threading

import pandas as pd
import soundfile as sf

sys.path.insert(0, str(pathlib.Path(__file__).parent.parent / "prep" / "src"))
from prep.slice import _to_samples
from datasets import Audio, load_dataset

CLIPS_PER_LANG = 15
MAX_CONCURRENT = 2  # 4-way concurrency was observed to make every language slower under
                     # contention, pushing genuinely-still-working downloads past the
                     # timeout and getting them wrongly marked as failed
LANGUAGE_TIMEOUT_SEC = 900  # 15 min
SAMPLE_SEED = 42

HERE = pathlib.Path(__file__).parent
OUT_DIR = HERE / "out"
CLIPS_DIR = OUT_DIR / "clips"
CLIPS_DIR.mkdir(parents=True, exist_ok=True)
CSV_PATH = OUT_DIR / "fleurs_metadata.csv"

languages = json.load(open(HERE.parent / "prep" / "languages.json"))["languages"]

# Resume support: a language only counts as "done" if it has a full set of metadata rows
# already in the CSV (not just leftover .wav files -- a language that timed out can still
# have partially/fully written .wav files on disk from the abandoned thread finishing
# slightly late, but with no row data ever recorded for it, so it must be retried).
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


def pull_language(lang, result):
    try:
        lang_id = lang["lang_id"]
        rng = random.Random(SAMPLE_SEED)

        ds = load_dataset("google/fleurs", lang_id, split="train", streaming=True)
        ds = ds.cast_column("audio", Audio(decode=False))

        reservoir = []
        for idx, example in enumerate(ds):
            if idx < CLIPS_PER_LANG:
                reservoir.append(example)
            else:
                j = rng.randint(0, idx)
                if j < CLIPS_PER_LANG:
                    reservoir[j] = example

        rows = []
        for i, example in enumerate(reservoir):
            samples, sample_rate = _to_samples(example["audio"])
            out_path = CLIPS_DIR / f"{lang_id}_{i:02d}.wav"
            sf.write(out_path, samples, sample_rate)

            rows.append({
                "lang_id": lang_id,
                "language": lang["language"],
                "country": lang["country"],
                "continent": lang["continent"],
                "difficulty": lang["difficulty"],
                "id": example.get("id"),
                "num_samples": example.get("num_samples"),
                "gender": example.get("gender"),
                "transcription": example.get("transcription"),
                "raw_transcription": example.get("raw_transcription"),
                "sample_rate": sample_rate,
                "file_path": str(out_path),
            })
        result["status"], result["rows"] = "ok", rows
    except Exception as e:
        result["status"], result["error"] = "error", e


failed_langs = []
completed = 0
state_lock = threading.Lock()
admission = threading.Semaphore(MAX_CONCURRENT)


def run_one(lang_num, lang):
    global completed
    lang_id = lang["lang_id"]
    result = {}
    inner = threading.Thread(target=pull_language, args=(lang, result), daemon=True)
    inner.start()
    inner.join(timeout=LANGUAGE_TIMEOUT_SEC)

    with state_lock:
        if inner.is_alive():
            failed_langs.append(lang_id)
            print(f"[{lang_num}/{len(languages)}] {lang_id}: TIMED OUT after {LANGUAGE_TIMEOUT_SEC}s -- skipping", flush=True)
        elif result.get("status") == "ok":
            rows.extend(result["rows"])
            print(f"[{lang_num}/{len(languages)}] {lang_id}: pulled {len(result['rows'])} clips", flush=True)
        else:
            failed_langs.append(lang_id)
            print(f"[{lang_num}/{len(languages)}] {lang_id}: FAILED ({result.get('error')!r}) -- skipping", flush=True)

        completed += 1
        pd.DataFrame(rows).to_csv(CSV_PATH, index=False)
        print(f"  progress: {completed}/{len(languages) - len(already_done)} remaining languages processed", flush=True)

    admission.release()


if __name__ == "__main__":
    dispatchers = []
    for lang_num, lang in enumerate(languages, start=1):
        if lang["lang_id"] in already_done:
            continue
        admission.acquire()
        th = threading.Thread(target=run_one, args=(lang_num, lang), daemon=True)
        th.start()
        dispatchers.append(th)

    for th in dispatchers:
        th.join()

    if failed_langs:
        print(f"\n{len(failed_langs)} language(s) failed/timed out: {failed_langs}")
    else:
        print("\nAll languages pulled successfully.")
