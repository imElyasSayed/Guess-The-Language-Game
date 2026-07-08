# Handoff prompt — wire the gameplay loop into the 3D tavern

Copy everything below the line into a fresh Claude Code chat opened in this repo.

---

I'm working on **"Say Again?"**, a Unity 6 (6000.0.30f1, macOS/Metal) multiplayer party
game: players listen to a short audio clip of someone speaking and race to guess the
accent/language ("Trust Your Ear" scoring — lock early for more points). Repo root:
`/Users/mohamedatwani/Desktop/Accent Guesser Game`, Unity project in `unity/`. Current
branch `feature/3d-world`. **Do not touch the `multiplayer` branch or `main`.**

## Where things stand

The 3D art migration is DONE. What exists:

- **3D tavern scene** — `unity/Assets/Scenes/Tavern.unity`, rebuilt by
  `Say Again ▸ Build 3D Tavern Scene` (`unity/Assets/Editor/TavernSceneBuilder.cs`). Cozy
  medieval tavern: round table, exactly 4 stools, a Liar's-Bar-style animal cast
  (`P1_Bulldog`, `P2_Giraffe`, `P3_Horse`, `P4_Fox` seated; `P5_Cat` + penguin
  `Announcer_Host` at the bar), warm point lighting, a framing camera. It is currently a
  **static environment — no gameplay is wired into it.** Pressing Play just shows the room.
- **Game logic — already built AND unit-tested** (`unity/Assets/Scripts/`):
  - `Core/GameController.cs` — pure single-player phase machine (Setup→Round→Reveal), no
    MonoBehaviour. Tests in `Assets/Tests/EditMode/GameControllerTests.cs`.
  - `Core/MatchController.cs` — pure host-authoritative multiplayer state machine, 2–8
    players, rotating asker, "Trust Your Ear" scoring, sole owner of the hidden target.
    Tests in `MatchControllerTests.cs`.
  - `Core/` also: `Player`, `Round`, `RoundFactory`, `ClipInfo`, `ScoreCalculator`,
    `Lang`, `IClipCatalog`/`JsonClipCatalog` (reads `Assets/StreamingAssets/clips.json`).
  - `Services/` — `AudioService`, `IOracleClient` (`MockOracleClient` offline default +
    `HttpOracleClient`).
  - `Net/MatchNetworkBehaviour.cs` — the ONLY netcode seam. Host owns the
    `MatchController`; clients send intents via ServerRpc; host broadcasts a redacted
    `RoundView` + hint, and the answer only at REVEAL, via ClientRpc. **It exposes C#
    events the presentation subscribes to:** `OnRoundView(RoundView)`,
    `OnHint(question, answer)`, `OnReveal(RoundResultView)`. `Net/NetworkBootstrap.cs` is
    a throwaway IMGUI lobby/HUD test driver.
- **Both existing UIs are explicitly throwaway scaffolding** meant to be replaced by the 3D
  tavern: `App/GameManager.cs` + `App/UiBuilder.cs` (basic uGUI buttons/text for
  single-player) and the IMGUI in `NetworkBootstrap`. Read their class docs — they say so.

So the logic and the netcode seam already exist and are tested. **The missing piece is a 3D
presentation layer that drives the loop inside the tavern scene instead of the placeholder
UI.**

## Your task

Build the **gameplay loop in the 3D tavern** so pressing Play actually plays a round.

Recommended approach — start with a **single-player vertical slice**, then layer multiplayer:

1. **Read first** (in this order): `Core/GameController.cs`, `App/GameManager.cs` +
   `App/UiBuilder.cs` (to see exactly how the loop is currently driven and reuse that
   wiring), `Core/MatchController.cs`, `Net/MatchNetworkBehaviour.cs`, and
   `unity/Assets/Editor/TavernSceneBuilder.cs` (how the scene + named animals are built).
   Also skim `docs/3d-tavern/README.md`.
2. **Design a `TavernPresenter` MonoBehaviour** (App layer) that lives in the tavern scene
   and drives a round on the 3D stage: a diegetic or minimal-overlay **Play/Replay clip**
   button (audio via `AudioService`), a **guess** input, a **lock** action, the **hint**
   reveal, the **answer reveal**, and **per-player score** display. Map the 4 seated animals
   to player slots (the seat/name mapping is in `TavernSceneBuilder`), and light up / react
   on the animal whose turn it is (e.g. a highlight, or reuse the giraffe `BadBreathToggle`
   pattern for simple per-character effects).
3. **Reuse, don't rewrite, the Core logic.** Presentation reads state and sends
   intents; it must never hold the hidden target or duplicate scoring. For multiplayer,
   subscribe to `MatchNetworkBehaviour`'s `OnRoundView`/`OnHint`/`OnReveal` events exactly
   as the design intends — do not bypass the host-authoritative model.
4. Add the presenter to `Tavern.unity` **through the scene builder**
   (`TavernSceneBuilder.cs`) so the scene stays regenerable — don't hand-edit the .unity
   YAML. Keep the old `GameManager`/`NetworkBootstrap` placeholders intact as fallback.

## Constraints & conventions

- **Don't modify** the pure `Core/` logic or its tests unless you find a real bug (if you
  do, add/adjust a unit test). Keep the answer-secrecy (host-only target) model intact.
- Follow the existing code style, namespaces (`AccentGuesser.App/Core/Net/Services`), and
  the "scene is built programmatically" pattern (`*SceneBuilder.cs` in `Assets/Editor/`).
- Accessibility per the brief: **no autoplay** — clips play only on a button; controls
  keyboard-navigable.
- This is a Steam-bound multiplayer game using Unity Netcode + Relay; keep it lightweight.

## How to verify (I run the interactive Editor; you can run batch mode)

- Edit-mode tests already exist; keep them green.
- Batch build the scene headless to check it compiles + assembles:
  `"/Applications/Unity/Hub/Editor/6000.0.30f1/Unity.app/Contents/MacOS/Unity" -batchmode
  -quit -projectPath "$(pwd)/unity" -executeMethod
  AccentGuesser.EditorTools.TavernSceneBuilder.Build -logFile /tmp/build.log`
- There's a headless capture at `AccentGuesser.EditorTools.TavernCapture.CaptureShot`
  (writes `blender/out/previews/UNITY_tavern.png`) for a visual sanity check.
- I'll do the actual "press Play and play a round" test in the Editor and report back.

Start by reading the files above and proposing a short plan for the single-player slice
before writing code. Ask me anything ambiguous about the round UX in the 3D space.
