import io
import tarfile
from dataclasses import dataclass
from typing import Iterable

import numpy as np
import soundfile as sf


@dataclass
class RawClip:
    samples: np.ndarray
    sample_rate: int
    transcription: str


def _fleurs_stream(lang_id: str) -> Iterable[dict]:
    """Stream FLEURS examples for a language from the HuggingFace Hub.

    Reads the dataset's ORIGINAL layout (per-language train.tsv + train.tar.gz of
    individual .wav files) instead of the HF-auto-converted parquet. The parquet has one
    giant row group per language with no page index, so `datasets` streaming, pyarrow
    range-reads, DuckDB, and the datasets-server /rows API are all forced to fetch close
    to the ENTIRE multi-hundred-MB file just to read a handful of rows (verified
    empirically). The tar.gz route lets us decompress on the fly and stop -- closing the
    connection -- as soon as the caller's `limit` is reached in `slice_clips`, so pulling
    e.g. 15 clips costs ~10MB instead of ~700MB+.

    Uses the ambient `huggingface_hub` token (via `hf auth login`) if present, for higher
    rate limits.
    """
    import requests
    from huggingface_hub import get_token

    base = f"https://huggingface.co/datasets/google/fleurs/resolve/main/data/{lang_id}"
    session = requests.Session()
    token = get_token()
    if token:
        session.headers["Authorization"] = f"Bearer {token}"

    tsv_resp = session.get(f"{base}/train.tsv", timeout=60)
    tsv_resp.raise_for_status()
    transcriptions: dict[str, str] = {}
    for line in tsv_resp.text.splitlines():
        parts = line.split("\t")
        if len(parts) >= 4:
            transcriptions[parts[1]] = parts[3]  # filename -> transcription

    tar_resp = session.get(f"{base}/audio/train.tar.gz", stream=True, timeout=60)
    tar_resp.raise_for_status()
    try:
        raw = _ResponseRawIO(tar_resp)
        tf = tarfile.open(fileobj=io.BufferedReader(raw), mode="r|gz")
        for member in tf:
            if not member.name.endswith(".wav"):
                continue
            wav_bytes = tf.extractfile(member).read()
            basename = member.name.rsplit("/", 1)[-1]
            yield {
                "audio": {"bytes": wav_bytes, "path": basename},
                "transcription": transcriptions.get(basename, ""),
            }
    finally:
        tar_resp.close()


class _ResponseRawIO(io.RawIOBase):
    """Adapts a streaming `requests.Response` into a raw file object for `tarfile`."""

    def __init__(self, resp):
        self._it = resp.iter_content(chunk_size=256 * 1024)
        self._buf = b""

    def readable(self) -> bool:
        return True

    def readinto(self, b) -> int:
        while len(self._buf) < len(b):
            try:
                self._buf += next(self._it)
            except StopIteration:
                break
        n = min(len(b), len(self._buf))
        b[:n] = self._buf[:n]
        self._buf = self._buf[n:]
        return n


def _to_samples(audio: dict) -> tuple[np.ndarray, int]:
    """Return (samples, sample_rate) from a FLEURS audio field.

    Handles both the decode=False shape ({"bytes", "path"}) and an already-decoded
    shape ({"array", "sampling_rate"}) so tests can inject either.
    """
    if audio.get("bytes") is not None:
        data, sample_rate = sf.read(io.BytesIO(audio["bytes"]))
        return np.asarray(data), int(sample_rate)
    return np.asarray(audio["array"]), int(audio["sampling_rate"])


def slice_clips(lang_id: str, limit: int, source: Iterable[dict] | None = None) -> list[RawClip]:
    """Return the first `limit` clips for a language as decoded audio.

    `source` is injectable (an iterable of FLEURS-shaped example dicts) so the
    logic is unit-testable without a network pull.
    """
    stream = source if source is not None else _fleurs_stream(lang_id)
    clips: list[RawClip] = []
    for example in stream:
        samples, sample_rate = _to_samples(example["audio"])
        clips.append(
            RawClip(
                samples=samples,
                sample_rate=sample_rate,
                transcription=example.get("transcription", ""),
            )
        )
        if len(clips) >= limit:
            break
    return clips
