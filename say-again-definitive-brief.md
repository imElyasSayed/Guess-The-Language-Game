# "Say Again?" — Definitive Build Brief (Unity / Steam / Multiplayer)

*The single authoritative specification. Build from THIS document. It commits to Unity, full-3D, local bundled audio, online multiplayer lobbies, a free-form insulting/funny/honest LLM oracle, and a tiny hosted relay for the API key. Where any earlier draft (yes/no oracle, browser artifact, Godot, backend-streamed audio, PC-as-server) conflicts, this document governs.*

---

## 0. Committed decisions (read first)

| Decision | Choice | Why |
|---|---|---|
| Engine | **Unity 6 LTS, C#, URP (full 3D)** | Best AI-training-data coverage (Claude writes more reliable Unity code), largest asset store for buying 3D art, proven Steam shipping. |
| Art | **Full 3D via bought asset packs** + hand-authored hero pieces | Claude can't model 3D; buy the bulk (Synty/Kenney), author only signature assets. |
| Audio | **Bundled locally as Ogg Vorbis** | Instant playback, no streaming. Total size is tiny (~60–400 MB — a non-issue for a game). |
| Multiplayer | **Core to v1.** Netcode for GameObjects + Facepunch.Steamworks (Steam relay + lobbies) | Friend invites / joinable lobbies; host-authoritative. |
| Oracle | **Free-form LLM**, insulting/funny/honest personality | Players ask anything; the Keep answers truthfully with attitude, never reveals the answer. |
| Oracle hosting | **Tiny hosted relay** (Cloudflare Worker / Vercel / Deno Deploy), NOT your PC | Holds the API key + runs leak-filter server-side; free tier covers a small launch. |
| Answer secrecy | **Host-authoritative** | Host holds the answer, calls the relay, broadcasts filtered text; clients never see the answer or call the LLM. |

---

# PART I — THE GAME

## 1. Pitch & tone

You're at a grimy tavern table with a crowd of shifty animal characters, rendered in stylized 3D at a cinematic top-down angle. A voice speaks in some language of the world. Each player gets one question to the rude hooded bartender ("The Keep") — he answers honestly but never gives the identity away — then everyone guesses the language. Play online with friends in joinable Steam lobbies. Tone: Liar's Bar — conspiratorial, warm-but-shady, lantern-lit.

## 2. The round loop

1. **A voice speaks** — a short FLEURS speech clip plays from the table centerpiece (bundled locally).
2. **Each player asks the Keep one question** — free-form, about the language's country/culture/geography. Answered honestly, never revealing the identity. One question per player per round.
3. **Everyone guesses** the language from options on the table.
4. **Reveal** — correct language + country/region; the table reacts; scores update.

Scoring: correct guesses score; guessing **without** asking earns a bonus (e.g. +15 vs +10) — core intent, and it feeds the "Trust Your Ear" achievement (§13).

---

# PART II — TECH STACK & ARCHITECTURE

## 3. Stack (Unity, Steam desktop, multiplayer)

| Concern | Choice | Notes |
|---|---|---|
| Engine | **Unity 6 LTS** | Current LTS at build time; C#. |
| Render | **URP, 3D** | Real geometry/lighting/depth; seats are world-space transforms. |
| Camera | **Cinemachine** | Fixed cinematic top-down-at-an-angle; slight idle drift for life. |
| Animation | **Animator + Timeline** | State machines for character reactions; Timeline for reveal/correct/wrong sequences. |
| Audio | **Unity Audio + `AudioSource.GetSpectrumData` (FFT)** | Playback of local Ogg clips + spectrum data drives the centerpiece "speaking" pulse. |
| UI | **UI Toolkit (or uGUI)** | Setup/lobby, question panel, guess tray, reveal, HUD as overlays on the persistent 3D scene. |
| **Multiplayer transport** | **Netcode for GameObjects (NGO) + Facepunch.Steamworks** | Steam relay for connections, Steam lobbies for invites/browser. Host-authoritative. |
| Steam features | **Facepunch.Steamworks** | Lobbies, friend invites, achievements, leaderboards. |
| Oracle networking | **UnityWebRequest → your hosted relay** | Host calls the relay; no secrets on any client. |
| Local data | **SQLite (or bundled JSON) + Ogg files** | Ships inside the game; clip metadata + audio on every machine. |
| Persistence | Local save + Steam stats | Settings/progress in `Application.persistentDataPath`; scores to Steam leaderboards. |

**Never** hardcode an Anthropic key, HuggingFace token, or any secret in the client build — it will be extracted. Secrets live only in the relay (§10).

## 4. Scene graph & systems

```
GameManager (persistent)             // state machine: Setup | Round | Reveal ; drives phase sync
 ├─ TavernScene (never reloaded across phases)
 │   ├─ Table (3D mesh)
 │   ├─ Seat × N                     // 3D transforms around the table; one per lobby player
 │   │   ├─ AnimalCharacter          // rigged model; Animator: idle | listening | reacting
 │   │   └─ Nameplate                // world-space billboard label above head = player name
 │   ├─ BartenderSeat                // "The Keep", dimmer warm key light
 │   ├─ Centerpiece                  // speaks the clip; spectrum-reactive
 │   ├─ TableClutter                 // lantern, cards, coins — atmosphere
 │   └─ SceneLighting                // lantern glow, ember flicker, reaction swells
 ├─ UI (screen-space overlays)
 │   ├─ SetupLobbyScreen             // difficulty + region + lobby (invite/browser)
 │   ├─ QuestionPanel                // player's one free-form question → Keep's answer
 │   ├─ GuessTray                    // language options as cards
 │   ├─ RevealOverlay                // language + country/region reveal
 │   └─ Hud                          // score, streak, round, players
 └─ Services (plain C# managers)
     ├─ NetworkService               // §9: NGO + Steam lobby/relay, host authority
     ├─ OracleClient                 // §10: calls your hosted relay
     ├─ ClipService                  // §7: loads local Ogg by clip id, spectrum data
     ├─ SteamService                 // §13: init/shutdown, achievements, leaderboards
     └─ SaveService                  // local progress/settings
```

`GameManager` owns state; systems read and raise events. `TavernScene` is always loaded so phases change overlays/Animator states, not the environment. In multiplayer the **host's** `GameManager` is authoritative and syncs phase + round data to clients.

---

# PART III — DATA (LOCAL) & PIPELINE

## 5. The core idea: audio ships locally; the network only coordinates

Every player installs the full clip library with the game. In a round, the **host picks a clip and broadcasts its ID** ("Round N: clip #4712"); each client plays its **local** copy instantly. The **answer stays on the host** until reveal. Only tiny data crosses the wire: clip IDs, questions/answers, guesses, scores, timers. This gives instant audio, tight sync, trivial bandwidth, and cheat resistance.

## 6. Storage reality (your size worry)

FLEURS clips are short speech; as Ogg Vorbis ~30–80 KB each. 100 languages × 20 clips ≈ **60–150 MB**; × 50 clips ≈ **150–400 MB**. Smaller than most games' texture sets — **not a concern.** (WAV would be ~10× larger; that's why we ship Ogg.)

## 7. Runtime clip data (SQLite)

At runtime the game needs a clip + its origin, filterable by region/difficulty. One embedded **SQLite** file:

```sql
CREATE TABLE clips (
  id            INTEGER PRIMARY KEY,
  file          TEXT NOT NULL,      -- "clips/es_419_00412.ogg"
  lang_id       TEXT NOT NULL,      -- FLEURS id, e.g. "es_419"
  language      TEXT NOT NULL,      -- "Spanish"
  country       TEXT NOT NULL,      -- origin (the answer)
  continent     TEXT NOT NULL,
  transcription TEXT,
  difficulty    TEXT                -- "common" | "all"
);
```

Read with a Unity SQLite plugin (e.g. SQLite-net). `ClipService` resolves a clip id → loads the local `.ogg` into an `AudioClip` (via `UnityWebRequestMultimedia` on a `file://` path or a direct loader) → plays it and feeds `GetSpectrumData` to the centerpiece. If querying stays minimal, a bundled JSON manifest works too; SQLite is preferred for filtering.

## 8. Data pipeline (prepared ONCE, offline, by you)

Build-time only; the shipped game never does this.
1. **Pull FLEURS once, offline** from HuggingFace. **DuckDB is a fine prep tool** to slice the parquet and export — but it is NOT shipped.
2. **Decode audio to Ogg Vorbis** — one `.ogg` per selected clip. Never ship WAV.
3. **Build the SQLite DB** (schema §7): one row per clip, including origin country/region and difficulty.
4. **Generate per-language `forbidden` lists** (§11 Layer 1) for the oracle's leak filter.
5. **Bundle** the Ogg folder + SQLite (+ forbidden lists, which the relay also needs) into the build.

Language coverage: start ~30–35 languages spread across continents; structure the data (ScriptableObject or JSON + metadata map keyed off FLEURS `lang_id`, region = ISO country code) so it grows toward the full **102 FLEURS languages** without code changes. Guess options: 3 random non-target languages from the active pool, shuffled with the target.

---

# PART IV — MULTIPLAYER

## 9. Networking (NGO + Facepunch.Steamworks)

- **Transport:** Steam relay via Facepunch.Steamworks; Netcode for GameObjects rides on top. Players connect through Steam lobbies (friend invites + a public lobby browser) — no dedicated servers, no IPs exposed.
- **Topology:** host-authoritative listen-server (one player hosts; simplest for small party lobbies). Dedicated servers optional later.
- **Host authority:**
  - Host picks the clip each round; holds the correct answer and the per-round fact sheet.
  - Host broadcasts clip id + phase changes; clients play their local file.
  - Clients send their question and guess to the host (RPC/ServerRpc).
  - **Only the host calls the oracle relay** and broadcasts each filtered answer, so everyone sees identical text and no client can fabricate or peek.
  - Answer revealed to clients only at round end.
- **Sync surface:** phase, round number, active clip id, per-player question-used flag, each player's answer text, each player's guess, scores/streaks, timers. All small.
- **Lobby config:** host sets difficulty + region; broadcast to clients at lobby start.

## 10. Oracle networking — the tiny hosted relay (NOT your PC)

A minimal always-on cloud function you deploy once (Cloudflare Workers / Vercel / Deno Deploy — generous free tiers, ~$0–5/mo at small scale). It replaces "a server on my computer," which is not viable (your PC would need to be on, online, and public 24/7 for all players).

**Endpoint:** `POST /oracle`
- Body: `{ roundToken, questionText }`. The relay holds (or is given, keyed by `roundToken`) the round's fact sheet so the **question text is all the client must send** — the client never transmits the answer.
- The relay calls the **Anthropic Messages API** (key server-side): `model: "claude-sonnet-4-6"`, `max_tokens: 1000`, with the Keep system prompt (§11 Layer 2).
- The relay runs the **leak filter** (§11 Layer 3) server-side, then returns `{ answer }`.
- The relay owns **rate limiting** (per user/lobby) and cost caps.

Only the **host** client calls this; it then broadcasts the answer to the lobby (§9). Keeps answers consistent and keeps even the host's game process from holding the raw key.

---

# PART V — THE ORACLE ("The Keep")

## 11. Personality + guardrails

**What it is:** an LLM-powered answerer. Players ask **free-form** questions ("alphabet or characters?", "near an ocean?", "would I hear this at the World Cup?"); the Keep answers **naturally**, not yes/no.

**Personality — insulting, funny, and honest:**
- **Answer honestly.** Anything he says about the language/country is true; he never lies or invents facts (a false answer poisons the deduction).
- **Never reveal the identity.** He answers the question asked without naming or near-naming the language, country, capital, or any giveaway.
- The humor is in *how* he answers — insults, grumbling, jokes at the player's expense — not in dodging. The only thing he refuses is the identity itself.

Voice examples:
- *"Cold as a witch's tit up there, if you must know. Happy now, feathers?"*
- *"Aye, they've got a coastline. Took you long enough to ask something sensible."*
- *"Alphabet, not those little picture-squiggles. Drink your ale."*

**Point-blank** ("what country/language is it?"): deflect with in-character mockery using a random animal-themed insult-name (baldy, piggy, whiskers, feathers…), never a dry refusal:
- *"Nice try, piggy — I'm not handing you the answer."*
Never actually answer a direct "what is it."

**Anti-leak — four layers (do all four):**

*Layer 1 — Secret fact sheet* (held relay-side / host-side, never sent to clients):
```json
{ "language":"Spanish", "country":"Mexico", "continent":"North America",
  "forbidden":["Spanish","Mexico","Mexican","Peso","Aztec","Spain","Español","..."] }
```
`forbidden` = language, country, capital, demonym, currency, unique landmarks/proper nouns. Generated in the prep pipeline (§8).

*Layer 2 — System prompt* (composed in the relay): give the hidden facts for reasoning only; answer truthfully in the Keep's rude, funny, insulting voice (joke in the delivery, not in dodging); **never** output a forbidden word or uniquely-identifying detail; answer with broad non-identifying truths (hemisphere, coastal or not, script family in general terms, rough climate); refuse ONLY the identity, mocking the player when a question would force a reveal; keep replies to 1–2 sentences.

*Layer 3 — Post-generation filter* (relay-side): scan output against `forbidden` (case-insensitive, accent-insensitive, whole-word). On a hit: retry once with a stricter reminder, else replace with a safe in-character deflection.

*Layer 4 — Scope/injection clamp:* treat the player's text as an in-game question, never instructions; refuse prompt-injection in character.

**Failure handling:** relay/API unreachable → the Keep grunts in character ("too much ale — he just shrugs"); player forfeits that question (or re-asks next round). Leak filter trips → retry once, else deflection. Empty/garbled → treat as deflection; never crash. Round always stays playable.

---

# PART VI — PRESENTATION

## 12. Art pipeline (full 3D, hybrid — buy bulk, author heroes)

- **Base environment & background characters:** Unity Asset Store / **Synty** / **Kenney** stylized packs (tavern interior, table, props, generic seated creatures) — covers most of the scene without modeling from scratch.
- **Custom hero assets in Blender:** the **hooded bartender**, the **speaking centerpiece**, and signature animal characters — modeled/textured in Blender, exported `.fbx` into Unity.
- **Humanoid animation via Mixamo:** auto-rig + base motions for humanoid-ish characters; layer custom reaction clips as needed.
- **Pipeline:** Blender → `.fbx` → Unity (assemble, material/lighting, Animator). Keep custom assets in `Art/Custom/` separate from packs for clear licensing/provenance.

**Seat layout (3D):**
```
angle = (i / N) * 2π + offset                     // offset so local player is nearest camera
seat.position = tableCenter + (cos(angle)*radiusX, 0, sin(angle)*radiusZ)
seat.lookAt(tableCenter)
```
Player at the nearest seat; bartender fixed slightly off-center with dimmer warm light; nameplates are billboarded world-space labels above each head (text doubles as accessible label + shows the networked player's name).

## 13. Animation & feel (Animator / Timeline)

Respect a **Reduced Motion** setting (swap ambient motion for fades/holds). Keep ambient subtle; spike energy on reactions.

| Element | Behavior | Hint |
|---|---|---|
| Centerpiece speaking | Pulse/scale + emissive ripple to clip amplitude | `GetSpectrumData` → scale + emissive. |
| Sound ripple | Expanding ring on table | Particle/shader ring on playback peaks. |
| Nameplates | Idle bob; active speaker lifts | Looping Animator; raise Y on active. |
| Characters listening | Head-cock to centerpiece | Animator `idle → listening`. |
| Keep thinking | Ember brighten, lantern dim | Light + emissive tween while awaiting relay. |
| Keep answer | Typewriter + micro-shake | UI typewriter; light flourish. |
| Guess played | Card slides to center | Timeline / tween. |
| Correct | Animals thump, coins bounce, warm swell | Staggered triggers + Timeline light swell. |
| Wrong | Groan wobble, red flicker, target nameplate glints | Short keyframed sequence. |
| Ambient | Lantern flicker, dust motes | Looping low-amplitude; off under Reduced Motion. |

Palette: lantern gold `#d8a53a`, tavern shadow `#141b2e`, worn teal `#1f6f6b`, ember coral `#e0644a`, aged paper `#f6f3ec`. Single-lantern lighting: warm central pool falling to shadowed edges. Worn display face for title/Keep dialogue; clean readable face for options.

## 14. Steam integration (Facepunch.Steamworks)

Init on boot, shutdown on quit; guard calls behind an initialized check so dev builds run outside Steam. Include `steam_appid.txt` for local testing.
- **Lobbies:** create/join, friend invites, public browser (also the multiplayer transport, §9).
- **Achievements** (define in the Steamworks partner backend): *First Pour* (first win), *Trust Your Ear* (correct without asking), *Silver Tongue* (streak of N), *Cellar of Tongues* (M distinct languages), extend per design.
- **Leaderboards:** global + friends for high score and best streak; **validate submissions through the relay** to deter tampering; display top/friends/around-user slices.

## 15. Accessibility & polish

Reduced Motion honored; visible audio **replay** control (first clip plays on a "deal me in" action, not on load); full **keyboard + controller** support with remappable controls and clear focus; **colorblind-safe** correct/wrong cues (never red/green alone); WCAG AA contrast on text/nameplates; nameplate names double as accessible character labels; **FLEURS CC-BY-4.0 attribution** in credits/about (§17).

---

# PART VII — EXECUTION

## 16. Build order

1. **Prep pipeline** (offline): FLEURS → DuckDB slice → Ogg + SQLite + forbidden lists.
2. **Single-player core:** `GameManager` state machine, `SetupScreen`, `ClipService` loads local Ogg, `GuessTray` + scoring + `RevealOverlay`. (Proves the data path with placeholder art.)
3. **Oracle:** deploy the relay; `OracleClient` → one free-form question, four-layer guardrails.
4. **Deploy the relay** properly (Cloudflare/Vercel/Deno) with rate limits.
5. **Multiplayer:** NGO + Facepunch.Steamworks — lobbies, friend invites, host authority; networked rounds (host picks clip → broadcast id → synced play → per-player questions via host → host-authoritative scoring + reveal).
6. **Steam:** achievements + leaderboards (validated via relay).
7. **Animation/art pass:** swap placeholders for hybrid 3D assets (§12); Animator/Timeline (§13); Reduced-Motion pass.
8. **Licenses screen** before submission (§17).

## 17. Licensing (mandatory before shipping)

FLEURS is **CC-BY-4.0**. Redistributing clips in a commercial Steam game is allowed **but requires attribution**: credit the **FLEURS** dataset (Google) and the underlying **FLoRes** text corpus, and state **CC-BY-4.0** in an in-game "Data & Licenses"/credits screen. Shipping clips without this violates the license — submission checklist item. Also track licenses/provenance for all bought asset packs (Synty/Kenney/Mixamo terms) in the same screen or an `Art/Custom/` provenance file.

## 18. Known risks

- **Scope.** A networked multiplayer 3D Steam game is a large project even with Claude writing code — you still wire scenes, import/assemble assets, test lobbies, and do Steam paperwork. Expect real months of work. **Mitigation:** follow the build order — get single-player fun first, then layer multiplayer.
- **Multiplayer is the hardest system.** NGO + Steam relay has real setup and edge cases (host migration, disconnects, late joiners). Budget time; consider disallowing mid-round joins in v1.
- **Full-3D art is the dominant cost.** Hybrid pipeline (§12) mitigates by buying bulk. **Fallback if budget bites:** drop to 2.5D (billboard sprites in the lit 3D scene) — backend, Steam, state, audio, multiplayer all unaffected.
- **Relay dependency & cost.** The oracle needs the relay online; budget the (small) hosting + Anthropic cost and rate-limit per user. Game degrades gracefully when the relay is unreachable.
- **FLEURS audio field shape** may vary; confirm against a live response during prep and fail loudly on undecodable clips.
- **Leaderboard anti-cheat:** validate through the relay, not the client.

## 19. Reference versions (current as of July 2026)

Unity 6 LTS · URP · Netcode for GameObjects · Facepunch.Steamworks · a Unity SQLite plugin · Ogg Vorbis audio · oracle model `claude-sonnet-4-6` (`max_tokens: 1000`) via the hosted relay · DuckDB (prep-time only, not shipped).

---

*End of definitive brief. This document governs; where any earlier draft conflicts (yes/no oracle, browser/React artifact, Godot, backend-streamed audio, PC-as-server), follow this one.*
