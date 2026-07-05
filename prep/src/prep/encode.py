from pathlib import Path

import numpy as np
import soundfile as sf


def clip_filename(lang_id: str, index: int) -> str:
    return f"clips/{lang_id}_{index:05d}.ogg"


def encode_ogg(samples: np.ndarray, sample_rate: int, out_path: str) -> None:
    """Write decoded audio samples to Ogg Vorbis via libsndfile.

    Ogg Vorbis is the format Unity imports natively. Input is a decoded sample
    array (float) plus its sample rate, as produced by the FLEURS stream.
    """
    if samples is None or len(samples) == 0:
        raise RuntimeError("refusing to encode empty audio")
    Path(out_path).parent.mkdir(parents=True, exist_ok=True)
    sf.write(out_path, samples, sample_rate, format="OGG", subtype="VORBIS")
