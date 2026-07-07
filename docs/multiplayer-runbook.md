# Multiplayer test runbook — Say Again?

Everything below the transport line is code and is already written. This runbook covers the
**in-editor wiring** (scene objects) and the **optional UGS link** (only needed for Unity Relay /
remote play) — the parts that can't be scripted headlessly. After this, you and a friend can play a
full match.

## What's already built

- **Pure rules core** — `MatchController` (roster of N, rotating asker, Trust-Your-Ear scoring,
  all-guessed-or-timer reveal, drop-in/disconnect). 17 passing unit tests in
  `Assets/Tests/EditMode/MatchControllerTests.cs`.
- **Netcode layer** (`Assets/Scripts/Net/`, assembly `AccentGuesser.Net`):
  - `RoundView.cs` — redacted wire types (no target before REVEAL).
  - `MatchNetworkBehaviour.cs` — host-authoritative wrapper (ServerRpcs in, ClientRpcs out).
  - `IConnectionManager.cs` — `DirectConnectionManager` (LAN/same-machine, zero setup).
  - `RelayConnectionManager.cs` — Unity Relay join-code (remote).
  - `NetworkBootstrap.cs` — IMGUI lobby + HUD; plays the full loop with **no uGUI prefab wiring**.
- Packages added to `Packages/manifest.json` (NGO, transport, Relay, auth, core). Unity resolves
  them on first open.

## Step 1 — Open the project

Open `unity/` in **Unity 6 LTS**. Let Package Manager resolve the new packages (first open takes a
minute). If a package version fails to resolve, open **Window ▸ Package Manager**, and update
`com.unity.netcode.gameobjects` / the `com.unity.services.*` packages to the versions your editor
offers — the code targets NGO 2.x and Services SDK current.

## Step 2 — Build the multiplayer scene (one-time, ~2 min)

1. **File ▸ New Scene**, save as `Assets/Scenes/Multiplayer.unity`.
2. Create an empty GameObject named **NetworkManager**:
   - Add component **NetworkManager**.
   - Add component **Unity Transport** (UnityTransport).
   - On NetworkManager, set **Network Transport** = the UnityTransport you just added.
3. Create an empty GameObject named **Match**:
   - Add component **NetworkObject**. (Scene-placed NetworkObjects auto-spawn on start — no prefab
     registration needed.)
   - Add component **MatchNetworkBehaviour**. (Set Round Seconds = 30, Reveal Seconds = 8.)
   - Add component **NetworkBootstrap**. It requires MatchNetworkBehaviour, so it's on the same GO.
     - **Manifest File** = `clips.json`
     - **Oracle Relay Base Url** = leave **blank** to use the offline mock Keep; or paste your relay
       URL to use the real oracle (host-only).
     - **Use Relay** = ON for remote friend, OFF for LAN / same-machine.
4. Make sure `Assets/StreamingAssets/clips.json` + the clips exist (same as single-player).
5. Add the scene to **File ▸ Build Settings** so builds include it.

## Step 3 — (Relay only) link a Unity Gaming Services project

Skip this entirely if you're testing with **Use Relay = OFF** (direct IP).

1. **Edit ▸ Project Settings ▸ Services** — sign in, create or link a UGS project (free).
2. In the Unity dashboard, enable **Relay** for the project. No code or keys needed; the
   `RelayConnectionManager` signs in anonymously and allocates automatically.

## Step 4 — Run two instances

You need two running copies. Easiest options:

- **Build + editor:** File ▸ Build, run the standalone as one player, press Play in the editor as the
  other.
- **Two editors:** install **ParrelSync** (clones the project) and open a second editor instance.

Then:

- **Direct IP (Use Relay OFF):** Host instance clicks **Host game** and shows `ip:port`. On the same
  machine the friend joins with `127.0.0.1:7777`; on the same LAN, use the shown IP.
- **Unity Relay (Use Relay ON):** Host clicks **Host game**, a **join code** appears. Friend types it
  into the join field and clicks **Join**.

Once connected, each instance shows the HUD: round number, roster+scores, the asker tag, **Play
clip**, the asker's **Ask the Keep** box, and **Lock guess** (early = +15, after the hint = +10). The
round advances on all-locked or the 30s timer, reveals for ~8s, then loops. Drop-in: a late joiner
enters at the next round; a disconnect stops gating the reveal.

## Step 5 — Verify answer secrecy

The whole point of host-authoritative here. On a **client** instance (not the host), the answer must
not exist until REVEAL:

- The `RoundView` a client receives has no target field — only `ClipId`, asker, phase, timer, and
  roster scores.
- The target language/country first appears in the `RevealClientRpc` payload at REVEAL.
- Guess **text** of other players is not visible until REVEAL (only their `HasLocked` flag is).

Quick check: put a breakpoint (or `Debug.Log`) in `NetworkBootstrap.RenderReveal` vs
`OnRoundView` — the target string is only ever populated in the reveal handler.

## Later: Steam

`RelayConnectionManager` and `DirectConnectionManager` implement `IConnectionManager`. A
`SteamConnectionManager` (Facepunch.Steamworks) implements the same `HostAsync()` / `JoinAsync()` and
drops in with nothing above the transport line changing — this is the brief's committed production
path once the game is otherwise ready.
