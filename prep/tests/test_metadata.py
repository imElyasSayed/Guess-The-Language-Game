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
