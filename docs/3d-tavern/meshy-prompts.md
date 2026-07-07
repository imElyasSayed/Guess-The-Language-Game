# Meshy.ai prompts — Beta Squad avatars + host

One prompt per model (Meshy generates a single character per job, ~600 char limit).
Each prompt carries the full game context so Meshy knows what the asset is for.

## Settings for every job

- Mode: **Text to 3D** · Art style: **Cartoon / Stylized**
- Topology: **Quad** · Target polycount: **10–20k**
- Symmetry: **On** · Pose: **T-pose** (for rigging later)
- Texture-stage prompt add-on: `flat hand-painted colors, cel-shaded look, warm tavern
  palette, no realistic skin texture`
- Export **FBX** → `unity/Assets/Art/Characters/generated/` with the same filenames
  (`P1_Chunkz.fbx` … `Announcer_Host.fbx`) → re-run *Say Again ▸ Build 3D Tavern Scene*.

## Shared context (already included in each prompt)

Playable character for "Say Again?" — a multiplayer party game set in a cozy medieval
tavern (Liar's Bar vibe). Players sit on stools around a round wooden table under warm
candle and lantern light; the camera views them from mid-distance, so faces and signature
features must read big and clear. Stylized cartoon, exaggerated proportions, distinct
silhouettes, funny not realistic.

## Prompts

### P1_Chunkz
Playable character for a cozy medieval-tavern multiplayer party game (Liar's Bar vibe),
seen at mid-distance sitting at a round pub table, so features must read big and clear.
Stylized cartoon man, huge almost-spherical round body, tiny stubby arms and legs, round
head with chubby cheeks and double chin, huge open grin with two big front teeth and a wide
gap between them, chin-strap beard, black baseball cap with messy blonde wig fringe under
the brim, black hoodie with white drawstrings, dark joggers. Funny, exaggerated, distinct
silhouette, T-pose, low-poly game-ready.

### P2_Niko
Playable character for a cozy medieval-tavern multiplayer party game (Liar's Bar vibe),
seen at mid-distance at a round pub table, so features must read big and clear. Stylized
cartoon man with a comically long giraffe-like neck, small head high above the shoulders,
skinny body, dark navy suit with white shirt and gold tie, bright green rectangular
sunglasses, short black curly high-top fade, chin-strap beard, goofy big-teeth grin, long
thin limbs, black dress shoes. Funny, exaggerated, distinct silhouette, T-pose, low-poly
game-ready.

### P3_Kenny
Playable character for a cozy medieval-tavern multiplayer party game (Liar's Bar vibe),
seen at mid-distance at a round pub table, so features must read big and clear. Stylized
cartoon boxer, lean athletic build, shirtless with thin gold chain, big red boxing gloves,
black boxing shorts with red waistband, red boxing boots with white laces, dark brown skin,
short black hair with a comically sharp straight geometric hairline pushed high up the
forehead, thin goatee, confident but clueless face. Funny, exaggerated, T-pose, low-poly
game-ready.

### P4_Sharky
Playable character for a cozy medieval-tavern multiplayer party game (Liar's Bar vibe),
seen at mid-distance at a round pub table, so features must read big and clear. Stylized
cartoon man, slim build, black oversized hoodie with small gold chest patch, blue jeans,
white sneakers, backwards black baseball cap, full thick black beard covering jaw and
cheeks, friendly easy grin, brown skin. Funny caricature, exaggerated cartoon proportions,
distinct silhouette, T-pose, low-poly game-ready character.

### P5_AJ
Playable character for a cozy medieval-tavern multiplayer party game (Liar's Bar vibe),
seen at mid-distance at a round pub table, so features must read big and clear. Stylized
cartoon man, slim, huge voluminous dark curly hair with golden-blonde dyed curl tips, wide
expressive eyes, thin goatee, silver chain necklace, grey checked blazer over white
open-collar shirt, black jeans, white sneakers. Funny caricature, exaggerated proportions,
distinct silhouette, T-pose, low-poly game-ready character.

### Announcer_Host
NPC host for a cozy medieval-tavern multiplayer party game (Liar's Bar vibe): the barkeep
announcer who stands behind the bar, introduces rounds and hypes winners, so he must look
larger-than-life and instantly read as the host. Stylized cartoon barkeep, huge round
belly, maroon vest with gold buttons over white shirt with rolled-up sleeves, small bowtie,
bald with side-hair ring, giant curled handlebar mustache, bushy brows, gold monocle, towel
on shoulder, black trousers and boots, jolly grin. T-pose, low-poly game-ready.

### Prop: beer mug (separate job — a held mug breaks T-pose rigging)
Wooden beer mug prop for a cozy medieval-tavern party game, foamy overflowing head, torus
handle, chunky cartoon proportions, low-poly stylized game-ready prop.

## Gotchas

- Tiny features (teeth gap, monocle chain, curl tips) come from the **texture** stage, not
  the mesh — repeat them in the texture prompt during Refine.
- One character per job; never combine.
- Check scale in Blender first: our world is 1 unit = 1 m, characters ~1.7–2.6 m tall
  (Niko tallest ~2.6 m with the neck, Chunkz shortest and widest ~1.9 m).
- Niko's breath toggle looks for a mesh named `BadBreath` inside his model — re-add a small
  green icosphere puff in Blender (in front of the mouth) before exporting, or keep the
  procedural Niko for that feature.
