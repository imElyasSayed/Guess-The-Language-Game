# Say Again? — Oracle Relay ("The Keep")

A tiny hosted HTTP service that answers players' free-form questions in the voice
of **The Keep** — a rude, funny, but always-honest hooded bartender — without
ever revealing the hidden language/country of the round. This implements §10 (the
relay) and §11 (personality + the four anti-leak layers) of the definitive brief.

The trusted host holds the round's answer, calls this relay with the player's
question + the round's fact sheet, and broadcasts the filtered reply. Clients
never call Anthropic and never see the key.

## Endpoint contract

### `POST /oracle`

Request body:

```json
{
  "question": "Would I hear this at the World Cup?",
  "factSheet": {
    "language": "Spanish",
    "country": "Mexico",
    "continent": "North America",
    "forbidden": ["spanish", "mexico", "mexican", "peso", "espanol"]
  }
}
```

Response (always HTTP **200** for a well-formed request, even if Anthropic fails):

```json
{ "answer": "Aye, you might, if you bothered to listen. Now drink up, feathers." }
```

- Invalid body (bad JSON, missing `question`, malformed `factSheet`) → **400**.
- Anthropic error/timeout → **200** with a gruff fallback line, so play continues.

The `factSheet` is exactly the shape produced by the `prep/` pipeline's per-language
`forbidden` fact sheets (`forbidden` = normalized lowercase words: language,
country, capital, demonym, currency, landmarks, proper nouns).

> **Future enhancement (brief §10 `roundToken`):** instead of the host sending the
> whole fact sheet every request, the relay could hold fact sheets keyed by an
> opaque `roundToken` and accept `{ roundToken, question }` so the answer never
> crosses the wire. We ship the simpler host-sends-factSheet version now; see the
> note in `src/oracle.ts`.

### `GET /health`

`200 { "ok": true }`.

## The four anti-leak layers

1. **Fact sheet** (`src/types.ts`, request contract) — the `forbidden` list
   arrives per request and is treated as case-insensitive, accent-insensitive,
   whole-word tokens.
2. **System prompt** (`src/prompt.ts` → `buildSystemPrompt`) — a pure function
   that gives the hidden facts for reasoning only; instructs truthful answers in
   the Keep's rude/funny/insulting-but-honest voice (humor in delivery, never
   lying/inventing); forbids emitting any forbidden word or uniquely-identifying
   detail; steers to broad non-identifying truths (hemisphere, coastal-or-not,
   general script family, rough climate); deflects point-blank "what is it?" with
   a random animal-themed insult ("piggy", "feathers", "whiskers", "baldy") and
   never answers it; caps replies at 1–2 sentences.
3. **Post-generation filter** (`src/leakFilter.ts` → `containsForbidden` +
   `normalize`) — a pure function that scans output against the forbidden list,
   case-insensitive, accent-insensitive (NFKD decompose + strip marks), WHOLE-WORD
   (so "spain" does not trip on "explain"). On a hit, `askKeep` retries **once**
   with a stricter reminder appended; if it still leaks, the whole answer is
   replaced with a safe in-character deflection.
4. **Scope/injection clamp** (`src/prompt.ts`) — the system prompt tells the model
   to treat the player's text purely as an in-game question and to refuse embedded
   prompt-injection in character.

Orchestration of layers 2–4 lives in `src/oracle.ts` → `askKeep(question,
factSheet, client)`, with the Anthropic call behind the injectable `OracleClient`
interface. `makeAnthropicClient(apiKey)` wraps the real SDK (model
`claude-sonnet-4-6`, `max_tokens: 1000`).

## Robustness

- Anthropic error/timeout → HTTP 200 + `"The Keep just grunts — too much ale.
  Ask again next round."` — never a 5xx for oracle failures.
- Empty/garbled model output → treated as a deflection.

## File tree

```
relay/
├── api/oracle.ts        # Vercel serverless entry (thin wrapper over the core)
├── src/
│   ├── types.ts         # FactSheet + OracleClient interfaces
│   ├── prompt.ts        # Layer 2 — buildSystemPrompt (pure)
│   ├── leakFilter.ts    # Layer 3 — containsForbidden + normalize (pure)
│   ├── oracle.ts        # askKeep orchestration + makeAnthropicClient
│   ├── server.ts        # POST /oracle + GET /health; injectable client
│   └── dev.ts           # local `npm run dev` entry
├── test/                # Vitest: prompt, leakFilter, oracle, server (fake client)
├── package.json
├── tsconfig.json
├── vitest.config.ts
├── .env.example
└── .gitignore
```

## Local run

```bash
cd relay
npm install
cp .env.example .env        # then put your real key in .env
set -a; source .env; set +a # export ANTHROPIC_API_KEY (bash/zsh)
npm run dev                 # http://localhost:8787
```

Smoke test:

```bash
curl -s localhost:8787/health
curl -s localhost:8787/oracle -H 'content-type: application/json' -d '{
  "question": "Is it near an ocean?",
  "factSheet": { "language":"Spanish","country":"Mexico","continent":"North America","forbidden":["spanish","mexico"] }
}'
```

## Test

```bash
npm test
```

Tests use a **fake** `OracleClient` — they never call the real Anthropic API.
Coverage: the prompt contains the facts + rules; the leak filter catches
forbidden words (including accented/case variants) and does not false-positive on
substrings; `askKeep` returns the answer on success, retries once then deflects
when the model leaks, and returns the fallback on client error; the HTTP layer
validates bodies and always 200s on oracle failure.

## Deploy (Vercel)

1. Ensure `@vercel/node` is not required — `api/oracle.ts` uses structural types.
2. In the Vercel project, set the env var **`ANTHROPIC_API_KEY`** (server-side
   only). Never put it in a client build.
3. Deploy the `relay/` directory. Vercel serves the function at `POST
   /api/oracle`. Point the game's `OracleClient` at that URL.

The secret lives only here. Rate limiting / cost caps (brief §10) are a natural
next addition at this boundary.
