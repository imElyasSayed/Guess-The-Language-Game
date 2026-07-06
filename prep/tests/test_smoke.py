import json
from pathlib import Path


def test_languages_json_has_the_full_fleurs_set():
    data = json.loads(Path("languages.json").read_text())
    ids = [lang["lang_id"] for lang in data["languages"]]
    assert len(ids) == 102
    assert len(set(ids)) == len(ids), "lang_ids must be unique"
    assert {"es_419", "ja_jp", "sw_ke"}.issubset(ids), "original seed languages must still be present"


def test_prep_package_importable():
    import prep  # noqa: F401
