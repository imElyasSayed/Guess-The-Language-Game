import json
from dataclasses import dataclass


@dataclass
class LanguageMeta:
    lang_id: str
    language: str
    country: str
    continent: str
    difficulty: str
    forbidden: list[str]


_REQUIRED = ("lang_id", "language", "country", "continent", "difficulty")


def load_metadata(path: str) -> tuple[int, list[LanguageMeta]]:
    data = json.loads(open(path, encoding="utf-8").read())
    count = int(data["clips_per_language"])
    langs = [
        LanguageMeta(
            lang_id=item["lang_id"],
            language=item["language"],
            country=item["country"],
            continent=item["continent"],
            difficulty=item["difficulty"],
            forbidden=list(item["forbidden"]),
        )
        for item in data["languages"]
    ]
    return count, langs


def validate(languages: list[LanguageMeta]) -> None:
    seen: set[str] = set()
    for lang in languages:
        for field in _REQUIRED:
            if not getattr(lang, field):
                raise ValueError(f"{lang.lang_id}: missing required field '{field}'")
        if lang.difficulty not in ("common", "all"):
            raise ValueError(f"{lang.lang_id}: difficulty must be 'common' or 'all'")
        if not lang.forbidden:
            raise ValueError(f"{lang.lang_id}: forbidden list must not be empty")
        if lang.lang_id in seen:
            raise ValueError(f"duplicate lang_id: {lang.lang_id}")
        seen.add(lang.lang_id)
