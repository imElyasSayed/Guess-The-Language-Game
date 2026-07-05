/**
 * Local dev entry: `npm run dev`.
 *
 * Reads ANTHROPIC_API_KEY from the environment (never hardcode a key) and
 * serves the same handler used in production/Vercel.
 */
import { createServer } from "./server.js";
import { makeAnthropicClient } from "./oracle.js";

const apiKey = process.env.ANTHROPIC_API_KEY;
if (!apiKey) {
  console.error(
    "ANTHROPIC_API_KEY is not set. Copy .env.example to .env and set it, then export it (e.g. `set -a; source .env; set +a`).",
  );
  process.exit(1);
}

const port = Number(process.env.PORT ?? 8787);
const server = createServer(makeAnthropicClient(apiKey));

server.listen(port, () => {
  console.log(`Say Again? oracle relay listening on http://localhost:${port}`);
  console.log(`  GET  /health`);
  console.log(`  POST /oracle   { question, factSheet }`);
});
