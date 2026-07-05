import json
from pathlib import Path


def test_languages_json_has_three_seed_languages():
    data = json.loads(Path("languages.json").read_text())
    ids = [lang["lang_id"] for lang in data["languages"]]
    assert ids == ["es_419", "ja_jp", "sw_ke"]


def test_prep_package_importable():
    import prep  # noqa: F401
