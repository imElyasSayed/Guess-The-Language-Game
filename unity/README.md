# Say Again? — Unity single-player core

The single-player core of "Say Again?" (brief §16 step 2): the round loop, scoring, local
audio, and the oracle client — with **BASIC placeholder uGUI only** (buttons + text). The
full 3D tavern, multiplayer, Steam, and the animation pass are deliberately **deferred**
(see "Intentionally deferred" below). The authoritative spec is
`../say-again-definitive-brief.md`; that document governs.

## Open in Unity 6

1. Install **Unity 6 LTS** (pinned to `6000.0.30f1` in `ProjectSettings/ProjectVersion.txt`;
   any 6000.x works — adjust if your installed patch differs).
2. Open this `unity/` folder as a project. Packages resolve from `Packages/manifest.json`
   (uGUI + Test Framework + the audio / UnityWebRequest / JSON modules).
3. Open a new empty scene, add an empty GameObject, and attach **`GameManager`**
   (`Assets/Scripts/App/GameManager.cs`). That is all the scene needs — `UiBuilder` constructs
   the placeholder canvas, an EventSystem, and an `AudioService` at runtime. Press Play.
   - "Deal me in" starts a round · "Play clip / Replay" plays the local Ogg (no autoplay) ·
     type a question + "Ask the Keep" (locks after one use) · pick one of 4 guess buttons ·
     the status line reveals the answer and updates score/streak.

## Project layout

```
unity/
├─ Assets/
│  ├─ Scripts/
│  │  ├─ Core/                      AccentGuesser.Core.asmdef — pure C#, no MonoBehaviour/scene deps
│  │  │  ├─ Lang.cs                 language + origin POCO (one per FLEURS lang_id)
│  │  │  ├─ ClipInfo.cs             one clip row (mirrors the SQLite clips table, camelCase)
│  │  │  ├─ IClipCatalog.cs         read model: GetLanguages / GetRandomClip + ClipFilter
│  │  │  ├─ JsonClipCatalog.cs      SIMPLE in-memory catalog (JSON manifest); SQLite swap noted inline
│  │  │  ├─ Round.cs                clip + target + shuffled choices
│  │  │  ├─ RoundFactory.cs         target + 3 distractors, shuffled; injected System.Random
│  │  │  ├─ ScoreCalculator.cs      +15 / +10 / +0 and streak rules (brief §2)
│  │  │  └─ GameController.cs       Setup|Round|Reveal state machine + one-question lock
│  │  ├─ Services/                  AccentGuesser.Services.asmdef — thin Unity-dependent
│  │  │  ├─ IOracleClient.cs        Task<string> AskAsync(question, target)
│  │  │  ├─ MockOracleClient.cs     canned gruff answers (offline default)
│  │  │  ├─ HttpOracleClient.cs     STUB POST to the relay's /oracle (UnityWebRequest)
│  │  │  └─ AudioService.cs         loads/plays local Ogg from StreamingAssets; GetSpectrumData amplitude
│  │  └─ App/                       Assembly-CSharp (auto-refs Core, Services, UnityEngine.UI)
│  │     ├─ GameManager.cs          boots catalog + controller, drives the uGUI loop
│  │     └─ UiBuilder.cs            builds the placeholder canvas at runtime
│  ├─ Tests/EditMode/               AccentGuesser.EditMode.asmdef (NUnit)
│  │  ├─ FakeClipCatalog.cs         test double for IClipCatalog
│  │  ├─ ScoreCalculatorTests.cs
│  │  ├─ RoundFactoryTests.cs
│  │  └─ GameControllerTests.cs
│  └─ StreamingAssets/
│     ├─ clips.json                 sample manifest (es_419, ja_jp, sw_ke, fr_fr placeholders)
│     └─ clips/.keep                drop the prep out/clips/*.ogg here
├─ Packages/manifest.json
├─ ProjectSettings/ProjectVersion.txt
├─ tools/db_to_clips_json.sh        derive clips.json from prep out/game.db
└─ README.md
```

## Where prep outputs go (StreamingAssets)

The prep pipeline (`../prep`) produces an `out/` folder with `clips/*.ogg`, `game.db`
(the `clips` table, brief §7), and per-language `forbidden/*.json` fact sheets. For the
shipped game these live under Unity `StreamingAssets/`:

- `out/clips/*.ogg`      → `Assets/StreamingAssets/clips/`
- `out/forbidden/*.json` → `Assets/StreamingAssets/forbidden/`  (used by the oracle later)
- `out/game.db`          → `Assets/StreamingAssets/game.db`      (once the SQLite swap lands)

Until then, regenerate the sample manifest from the DB with:

```bash
cd unity/tools
./db_to_clips_json.sh ../../prep/out/game.db ../Assets/StreamingAssets/clips.json
```

## JSON-manifest now / SQLite later

Brief §7 prefers an embedded **SQLite** `game.db` read via a Unity SQLite plugin
(e.g. SQLite-net) for richer region/difficulty filtering. To keep the single-player core
simple and fully unit-testable without a native plugin, the shipped-now catalog reads a
**`clips.json`** manifest (`JsonClipCatalog`). This is the sanctioned simple fallback the
brief explicitly allows ("If querying stays minimal, a bundled JSON manifest works too").

The swap is isolated: implement `IClipCatalog` over `game.db` and hand that instance to
`GameController` — no game-rule code changes. `tools/db_to_clips_json.sh` keeps the manifest
and the DB aligned in the meantime (single source of truth = `game.db`).

## Relay contract (HttpOracleClient)

`HttpOracleClient` is a **stub** matching the relay that is being built in parallel (brief §10).
It targets:

```
POST {relayBaseUrl}/oracle
request:  { "question": string,
            "factSheet": { "language": string, "country": string,
                           "continent": string, "forbidden": string[] } }
response: { "answer": string }
```

On any failure it returns a gruff in-character line so the round stays playable (brief §11).
The `forbidden` array comes from the per-language fact sheets in `StreamingAssets/forbidden/`
(wire `GameManager.ForbiddenFor` to read them when the relay is live). **No secrets in the
client** — the Anthropic key lives only in the relay (brief §3). Note: the brief also sketches
a leaner `{ roundToken, questionText }` client contract; this build task pinned the
fact-sheet-in-body shape above, so that is what is implemented. The default client is
`MockOracleClient` (offline); set `GameManager._relayBaseUrl` to use the real relay.

## How audio + catalog load from StreamingAssets

- **Catalog:** `GameManager` reads `StreamingAssets/clips.json` with `File.ReadAllText`, parses
  it via `JsonUtility` (a bare top-level array is wrapped as `{"clips":[...]}` since JsonUtility
  can't parse a top-level array), and builds a `JsonClipCatalog`.
- **Audio:** `AudioService.PlayClip` resolves a clip's `file` path (e.g. `clips/es_419_00001.ogg`)
  against `Application.streamingAssetsPath`, loads the Ogg via
  `UnityWebRequestMultimedia.GetAudioClip(..., AudioType.OGGVORBIS)`, and plays it through an
  `AudioSource`. `Replay()` restarts it; `GetAmplitude()` exposes a `GetSpectrumData`-based
  value for the future centerpiece pulse (brief §13). **No autoplay** — playback is button-driven
  (accessibility, §15).

## Running the EditMode tests

Tests for `RoundFactory`, `ScoreCalculator`, and `GameController` (deterministic via an injected
`System.Random`) live in `Assets/Tests/EditMode/`. Run headless:

```bash
Unity -batchmode -runTests -projectPath unity -testPlatform EditMode -quit
```

(Use the full path to your Unity 6 executable in place of `Unity`. Results are written to an
XML file; pass `-testResults path/to/results.xml` to choose the location.)

> These tests were **authored but NOT executed** here — no Unity Editor is available in the
> build environment. They are written to compile and pass under Unity 6.

## Intentionally deferred (not in this core)

- **3D art / tavern / characters / animation** (brief §12–§13) — placeholder uGUI stands in.
- **Multiplayer** (NGO + Facepunch.Steamworks, brief §9) — this is single-player only.
- **The oracle relay itself** (brief §10) — built separately; only the client stub is here.
- **Steam** (lobbies, achievements, leaderboards, brief §14).
- **SQLite catalog** (brief §7 preferred path) — JSON manifest is the current fallback.
- **Licenses screen** (FLEURS CC-BY-4.0 attribution, brief §17) — required before shipping.

Meta (`.meta`) files are omitted; Unity generates them on first import.
```
