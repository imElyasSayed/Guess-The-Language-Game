import json
from pathlib import Path
from prep.forbidden import normalize_forbidden, write_forbidden_files
from prep.metadata import LanguageMeta


def test_normalize_lowercases_strips_accents_dedupes():
    out = normalize_forbidden(["Español", "espanol", "  Mexico  ", "", "MEXICO"])
    assert out == ["espanol", "mexico"]


def test_write_forbidden_files_emits_fact_sheet(tmp_path):
    langs = [LanguageMeta("es_419", "Spanish", "Mexico", "North America", "common", ["Spanish", "México"])]
    write_forbidden_files(langs, str(tmp_path))
    sheet = json.loads((tmp_path / "es_419.json").read_text())
    assert sheet["language"] == "Spanish"
    assert sheet["country"] == "Mexico"
    assert "spanish" in sheet["forbidden"]
    assert "mexico" in sheet["forbidden"]
