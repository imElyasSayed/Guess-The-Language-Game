import io
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

    Streaming mode is used because the google/fleurs parquet has very large row
    groups (no page index), so both DuckDB range-reads and the datasets-server
    /rows API fail on it. We cast the audio column with decode=False so `datasets`
    hands us the raw encoded audio bytes instead of decoding them itself — this
    avoids the heavy `torchcodec` dependency that datasets>=5 requires for audio
    decoding. We decode the bytes locally with soundfile in `slice_clips`.
    """
    from datasets import Audio, load_dataset

    ds = load_dataset("google/fleurs", lang_id, split="train", streaming=True)
    return ds.cast_column("audio", Audio(decode=False))


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
