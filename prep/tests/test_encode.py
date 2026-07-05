import numpy as np
import soundfile as sf
import pytest
from prep.encode import encode_ogg, clip_filename


def tone(seconds=0.25, sr=16000):
    t = np.linspace(0, seconds, int(sr * seconds), endpoint=False)
    return (0.2 * np.sin(2 * np.pi * 440 * t)).astype("float32")


def test_clip_filename_zero_pads():
    assert clip_filename("es_419", 12) == "clips/es_419_00012.ogg"


def test_encode_produces_a_valid_ogg_vorbis(tmp_path):
    out = tmp_path / "clip.ogg"
    encode_ogg(tone(), 16000, str(out))
    assert out.exists() and out.stat().st_size > 0
    info = sf.info(str(out))
    assert info.format == "OGG"
    assert info.subtype == "VORBIS"
    assert info.duration > 0


def test_encode_raises_on_empty_audio(tmp_path):
    with pytest.raises(RuntimeError):
        encode_ogg(np.array([], dtype="float32"), 16000, str(tmp_path / "bad.ogg"))
