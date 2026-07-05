import json
import unicodedata
from pathlib import Path
from prep.metadata import LanguageMeta


def _strip_accents(text: str) -> str:
    decomposed = unicodedata.normalize("NFKD", text)
    return "".join(c for c in decomposed if not unicodedata.combining(c))


def normalize_forbidden(words: list[str]) -> list[str]:
    result: list[str] = []
    seen: set[str] = set()
    for word in words:
        norm = _strip_accents(word).lower().strip()
        if norm and norm not in seen:
            seen.add(norm)
            result.append(norm)
    return result


def write_forbidden_files(languages: list[LanguageMeta], out_dir: str) -> None:
    Path(out_dir).mkdir(parents=True, exist_ok=True)
    for lang in languages:
        sheet = {
            "lang_id": lang.lang_id,
            "language": lang.language,
            "country": lang.country,
            "continent": lang.continent,
            "forbidden": normalize_forbidden(lang.forbidden),
        }
        (Path(out_dir) / f"{lang.lang_id}.json").write_text(
            json.dumps(sheet, ensure_ascii=False, indent=2), encoding="utf-8"
        )
