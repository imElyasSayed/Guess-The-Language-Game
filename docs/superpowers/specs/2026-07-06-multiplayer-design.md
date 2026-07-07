# Say Again? — Multiplayer Design

**Date:** 2026-07-06
**Status:** Approved design, ready for implementation planning
**Supersedes:** nothing — extends the single-player core toward the brief's "multiplayer is core to v1" commitment.

## Context

The current codebase is a single-player core: a pure, MonoBehaviour-free
[`GameController`](../../../unity/Assets/Scripts/Core/GameController.cs) state
machine (`Setup → Round → Reveal`) driven by a uGUI
[`GameManager`](../../../unity/Assets/Scripts/App/GameManager.cs), with
`RoundFactory`, `JsonClipCatalog`, `ScoreCalculator`, `AudioService`, and an
`IOracleClient` (mock or HTTP relay). There is **no networking code yet**.

The definitive brief locks in: Netcode for GameObjects, **host-authoritative
listen-server**, host holds the answer + fact sheet, clients never call the LLM
or see the answer until reveal, only the host calls the oracle relay then
broadcasts the answer.

**Phasing decision (this spec):** Steam (Facepunch.Steamworks lobbies/invites)
is **deferred**. We ship a testable multiplayer build first using **Unity Relay
+ join code** for connection. Netcode for GameObjects stays; only the
transport/discovery layer differs. Steam later swaps in behind the same
interface with nothing above the transport line changing.

## Gameplay model (settled)

- **Round flow:** shared clip for the whole table; one **rotating asker** per
  round who may ask the Keep its single question. The question and answer are
  broadcast to everyone.
- **Scoring — per-player "Trust Your Ear":** any player may **lock in** a guess
  *early*, before the asker's Q&A is broadcast, and is eligible for **+15**;
  players who wait for the broadcast hint and then lock get **+10**. Wrong guess
  is **+0** and resets that player's streak. The tier is decided by lock
  **timing relative to the hint broadcast**, not by whether the player
  personally asked. (Solo collapses to today's behavior: the lone player is the
  asker, so "didn't ask" = +15.)
- **Match structure — endless / drop-in:** no fixed end. Players join and leave
  freely, rounds keep coming, a rolling leaderboard shows standings, the host
  can end the match anytime.
- **Reveal trigger — all-guessed-or-timer:** Reveal fires when every present
  player has locked a guess **OR** a per-round timer (default **30s**) expires,
  whichever comes first.
- **Lobby size:** 2–8 players.
- **First lock is final:** a player cannot change a guess once locked (prevents
  timer-gaming and tier-switching).

## Architecture & module boundaries

The rules core is lifted out from under the single-player MonoBehaviour and
generalized from one player to a roster. Everything below the netcode line stays
pure and unit-testable, exactly like `GameController` is today.

```
Core (pure, no MonoBehaviour, no netcode) — unit-tested
├── MatchController        roster of Players; owns the round lifecycle for N players
├── Player                 id, displayName, score, streak; per-round: lock state, guess, tier, points
├── RoundState             chosen ClipInfo, hidden Target, askerId, phase, hintPublic, timerDeadline
├── RoundFactory           unchanged — draws clip + target
└── ScoreCalculator        unchanged rules (+15 early / +10 after hint / +0 wrong)

Net (Unity, host-authoritative) — thin, NO rules logic
├── MatchNetworkBehaviour  wraps one host-side MatchController; ServerRpc in, NetworkVariable/ClientRpc out
├── RedactedRoundView      the client-visible view — NEVER contains the target or fact sheet
└── Oracle glue            host-only IOracleClient call; answer broadcast via ClientRpc

App (presentation)
├── GameManager            solo path = a 1-player MatchController running locally (host of itself)
├── ConnectionManager (new) transport-agnostic host/join; Unity Relay now, Steam later
└── UiBuilder              extended: roster panel, per-player lock indicators, leaderboard, join-code UI
```

**Core property:** `MatchController` **never** lives on a client. A client only
ever holds a `RedactedRoundView`. Single-player and the host run the *same*
`MatchController`; solo is just `roster.Count == 1` with the netcode layer
bypassed. This yields **one rules code path**, keeps the answer-secrecy model
intact, and preserves the existing pure/testable design.

## Round lifecycle (host-authoritative state machine)

```
SETUP ──host StartRound──▶ LISTEN ─────────────────────────▶ REVEAL ──▶ (next) LISTEN
                             │  host picks clip + hidden target
                             │  host assigns asker = roster[roundNo % activePlayers]
                             │  broadcasts RedactedRoundView + starts 30s timer
                             │
     per player, in any order until reveal:
       • LockGuess(text)  → early lock, +15 tier ("Trust Your Ear")
       • (asker only) AskQuestion(text) once → host calls Oracle,
         broadcasts Q + answer to table; hintPublic flips true
       • LockGuess(text) after hintPublic → +10 tier
                             │
     REVEAL fires when: every present player has locked  OR  timer hits 0
       host scores each locked guess vs target, updates score/streak,
       broadcasts full result incl. the revealed target, holds ~8s, loops to LISTEN
```

**Host-enforced rules (clients cannot cheat — they never hold the target):**

- **One question per round, asker only.** A non-asker's `AskQuestion` is
  rejected. The asker may decline; then no hint is broadcast and everyone
  guesses cold.
- **Tier by timing:** lock before `hintPublic` → +15 tier; after → +10 tier;
  wrong → +0 and streak reset.
- **Idle players:** a player who never locks scores +0 that round when the timer
  fires.
- **Disconnect mid-round:** the player is dropped from the roster and no longer
  gates reveal.
- **Drop-in joiners:** enter at the next `LISTEN` (cannot join mid-round),
  starting at score 0.
- **Asker rotation** skips players who have left, keeping asking roughly even.

## Networking contract & secrecy enforcement

The wire surface is deliberately tiny. Clients send *intents*; the host sends
*redacted facts*.

**Client → Host (ServerRpc; host validates every one):**
- `LockGuessServerRpc(string guess)` — records sender's guess + tier from the
  current `hintPublic` flag; **first lock wins**, later locks ignored.
- `AskQuestionServerRpc(string question)` — rejected unless `sender == askerId`
  and not yet asked this round.

**Host → Clients:**
- `RedactedRoundView` (NetworkVariables / synced struct):
  `clipId, askerId, phase, hintPublic, timerDeadline,
  roster[{id, name, score, streak, hasLocked}]`. **No target, no fact sheet, ever.**
- `BroadcastHintClientRpc(string question, string answer)` — fired once when the
  host resolves the asker's question; flips `hintPublic`.
- `RevealClientRpc(RoundResult)` — sent only at REVEAL; reveals `target` and each
  player's guess/points/newScore. **First and only moment the target crosses the wire.**

**Secrecy invariants:**
1. The target and fact sheet exist **only** in the host's `MatchController`
   memory — never in a NetworkVariable, never in a pre-reveal RPC.
2. Only the host constructs an `IOracleClient` and loads forbidden-word lists;
   clients have neither the relay URL nor the lists.
3. `hasLocked` replicates (UI shows who's ready) but **guess text does not**
   replicate until REVEAL, so no one can copy another player's answer.

A non-asker client has **no code path** to ask the Keep or read the answer — it
is structurally enforced, not merely UI-gated.

## Oracle in multiplayer

The existing `IOracleClient` barely changes. Only the host holds a real client
(mock or `HttpOracleClient` → relay). On `AskQuestionServerRpc`, the host
`await`s the answer then calls `BroadcastHintClientRpc(question, answer)`. The
relay round token / fact sheet stay host-side exactly as the brief specifies.
Clients construct no oracle client and load no forbidden lists.

## Connection layer — Unity Relay (`ConnectionManager`)

```
Host clicks "Host game"
  → UGS sign-in (anonymous) → allocate Unity Relay → get join code
  → NetworkManager starts host on UnityTransport(relay allocation)
  → display join code on screen
Friend clicks "Join", pastes code
  → UGS sign-in (anonymous) → join Relay by code → start client
  → on connect, host adds them to the roster at the next LISTEN
```

`ConnectionManager` exposes just two methods — `HostAsync() → joinCode` and
`JoinAsync(code)` — behind a transport-agnostic interface. When Steam lands
later, a `SteamConnectionManager` implements the same two methods and everything
above is untouched. Works over the internet with **no port forwarding**; cost is
a one-time free Unity Gaming Services project link.

**Unity packages to add:**
`com.unity.netcode.gameobjects`, `com.unity.transport`,
`com.unity.services.relay`, `com.unity.services.authentication`,
`com.unity.services.core`.

## Testing

- **Core stays pure & unit-tested.** New `MatchControllerTests` cover: roster
  scoring; asker rotation (including skip-on-leave); +15 vs +10 tier by lock
  timing relative to `hintPublic`; reveal-on-all-locked vs reveal-on-timer;
  first-lock-final; disconnect mid-round drops from roster and ungates reveal;
  drop-in joiner enters at next round at score 0. Same style as today's
  `GameControllerTests`, no netcode required.
- **Net layer:** a thin manual playtest checklist (host + client across two
  editor instances, or a build + editor) since NGO integration tests are
  heavyweight. **Secrecy assertion:** inspect a client's replicated state and
  confirm `target` is null until `RevealClientRpc` fires.
- **Solo regression:** the 1-player local path reproduces today's exact
  behavior; existing `GameControllerTests` / `RoundFactoryTests` keep passing
  (adapted if `GameController` is renamed/promoted to `MatchController`).

## Out of scope (explicitly)

- Steam lobbies, friend invites, public browser (deferred; swaps in behind
  `ConnectionManager` later).
- Fixed-length matches or first-to-N scoring (endless/drop-in only for now).
- Re-guessing / changing a locked guess.
- 3D tavern presentation and art (brief §12, deferred to the end as always).
- Persistence of scores across matches / accounts.

## Build order

1. Promote `GameController` → `MatchController` (roster of 1), keep solo path and
   all tests green. Pure core only.
2. Extend `MatchController` to N players: asker rotation, per-player lock/tier,
   all-guessed-or-timer reveal, drop-in/disconnect handling. Add
   `MatchControllerTests`.
3. Add NGO + Unity Relay packages; build `ConnectionManager` (host/join by code).
4. Build `MatchNetworkBehaviour`: ServerRpcs in, `RedactedRoundView` +
   `BroadcastHintClientRpc` + `RevealClientRpc` out. Wire host-only oracle.
5. Extend UI: join-code screen, roster panel, per-player lock indicators, live
   leaderboard.
6. Two-machine playtest + secrecy assertion.
```
