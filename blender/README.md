# 3D Asset Pipeline (Blender → Unity)

Procedural, code-driven low-poly assets for the **Say Again?** 3D tavern. Everything is
generated headlessly from Python — no hand-editing in the Blender UI — so the whole art
set is reproducible and diff-able.

## Regenerate everything

```bash
blender --background --python blender/build_all.py
```

Requires Blender 5.x (`brew install blender`). This:

1. Builds each asset in a clean scene (transforms baked, origin set, UV smart-projected).
2. Exports game-ready **FBX** into `unity/Assets/Art/` (native Unity import, no extra packages).
3. Writes `unity/Assets/Art/Env/tavern_lights.json` — warm point-light positions in Unity space.
4. Renders preview PNGs + an assembled hero shot to `blender/out/previews/` (git-ignored).

## Files

| File | What it makes |
|------|---------------|
| `gen_lib.py` | Headless-safe helpers: unit primitives, materials, join, UV, origin, FBX/GLB export, preview render. All transforms are baked into mesh data to avoid operator-context issues in background mode. |
| `gen_env.py` | Tavern kit: floor, walls, high beams, stone fireplace, bar + back-bar bottle shelves, barrels, hanging lanterns, wall candles, circular table, stool. `build_room()` joins the static shell into one `TavernRoom` mesh; Table/Stool/Barrel export separately. |
| `gen_characters.py` | The five Beta Squad avatars (Chunkz, Niko, Kenny, Sharky, AJ — from the reference photos in the repo root) + the announcer host. Each is logical parts (Head/Neck/Torso/Arms/Legs) parented to a feet-origin root so it stays riggable. |
| `build_all.py` | Orchestrator (build → UV → export → preview). |

## Conventions

- **Scale:** 1 Blender unit = 1 metre = 1 Unity unit.
- **Up:** +Z in Blender; FBX exported with `bake_space_transform` + Y-up so Unity imports 1:1.
- **Characters:** modelled facing +Y (→ Unity +Z forward), origin at the feet, parts kept separate.
- **Props:** joined to one mesh, origin at bottom-centre (base sits on the floor at the origin).

## Assets produced

- `unity/Assets/Art/Env/` — `TavernRoom.fbx`, `Table.fbx`, `Stool.fbx`, `Barrel.fbx`, `tavern_lights.json`
- `unity/Assets/Art/Characters/generated/` — `P1_Chunkz`, `P2_Niko`, `P3_Kenny`, `P4_Sharky`, `P5_AJ`, `Announcer_Host` (`.fbx`)

## Assemble in Unity

Menu **Say Again ▸ Build 3D Tavern Scene** (see `unity/Assets/Editor/TavernSceneBuilder.cs`)
lays the room + table + 4 seats/players + announcer, wires the warm lights, and frames the
camera into `Assets/Scenes/Tavern.unity`. Re-run any time you regenerate the art.
