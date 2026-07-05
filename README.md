# Say Again? (working title: Guess-The-Language-Game)

A language-guessing game with a Liar's Bar tavern aesthetic: a voice speaks in
some world language, you may ask one question to a rude-but-honest hooded
bartender ("The Keep"), then guess the language. Built in **Unity 6** for Steam,
with online multiplayer and an LLM oracle planned.

> **The single authoritative spec is [`say-again-definitive-brief.md`](say-again-definitive-brief.md).**
> It governs all build decisions.

## Repository layout

| Path | What it is | Status |
|---|---|---|
| `say-again-definitive-brief.md` | The definitive design + tech brief | authoritative |
| `prep/` | Offline data pipeline: FLEURS → Ogg Vorbis clips + SQLite `game.db` + per-language `forbidden` lists (Python, pytest) | ✅ built & tested; 3 languages pulled |
| `relay/` | Hosted oracle service: `POST /oracle`, the Keep's voice + 4-layer anti-leak filter (Node/TS, Vercel-deployable) | ✅ built; 31 tests pass |
| `unity/` | The game: single-player core — state machine, scoring, round loop, audio, placeholder uGUI (C#) | ✅ logic built & tested; runs pending Unity license + first import |
| `docs/superpowers/plans/` | Implementation plans | reference |
| `ATTRIBUTION.md` | FLEURS / FLoRes CC-BY-4.0 data attribution | required |

## Current state

A verified single-player vertical slice with **basic placeholder graphics**
(no 3D yet — deferred to the final art pass per brief §12). Real FLEURS audio
(Spanish / Japanese / Swahili, 30 clips each) is staged into
`unity/Assets/StreamingAssets/`.

**To run:** install Unity 6 (`6000.0.30f1`), activate a free Personal license,
open the `unity/` project, press Play. The oracle works offline via a mock
client; the live LLM oracle needs the `relay/` deployed with an `ANTHROPIC_API_KEY`.

## Deliberately deferred (later phases)

Online multiplayer (NGO + Steamworks), Steam achievements/leaderboards, the full
3D tavern/character art, and the in-game licenses screen. See brief §16 build order.

## Building the data yourself

See [`prep/README.md`](prep/README.md). Requires Python 3.11+; no ffmpeg needed
(encoding uses `soundfile`/libsndfile).
# Guess-The-Language-Game
