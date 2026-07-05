import io
import numpy as np
import soundfile as sf
from prep.slice import slice_clips, RawClip


def _wav_bytes(seconds=0.1, sr=16000):
    t = np.linspace(0, seconds, int(sr * seconds), endpoint=False)
    tone = (0.1 * np.sin(2 * np.pi * 440 * t)).astype("float32")
    buf = io.BytesIO()
    sf.write(buf, tone, sr, format="WAV")
    return buf.getvalue()


def fake_stream(n):
    """Mimics the decode=False FLEURS shape: audio = {'bytes', 'path'}."""
    for i in range(n):
        yield {
            "audio": {"bytes": _wav_bytes(), "path": f"clip_{i}.wav"},
            "transcription": f"line {i}",
        }


def test_slice_decodes_bytes_and_reads_transcription():
    clips = slice_clips("es_419", limit=2, source=fake_stream(5))
    assert len(clips) == 2
    assert isinstance(clips[0], RawClip)
    assert clips[0].sample_rate == 16000
    assert len(clips[0].samples) > 0
    assert clips[0].transcription == "line 0"


def test_slice_stops_at_limit_even_if_stream_is_longer():
    clips = slice_clips("es_419", limit=3, source=fake_stream(100))
    assert len(clips) == 3


def test_slice_returns_all_when_stream_shorter_than_limit():
    clips = slice_clips("es_419", limit=10, source=fake_stream(4))
    assert len(clips) == 4


def test_slice_accepts_already_decoded_array_shape():
    def decoded(n):
        for i in range(n):
            yield {
                "audio": {"array": np.array([0.0, 0.1, -0.1], dtype="float32"),
                          "sampling_rate": 8000},
                "transcription": f"t{i}",
            }
    clips = slice_clips("x", limit=2, source=decoded(3))
    assert clips[0].sample_rate == 8000
    assert list(clips[0].samples) == [0.0, 0.1, -0.1]
