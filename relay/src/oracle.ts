import Anthropic from "@anthropic-ai/sdk";
import { buildSystemPrompt } from "./prompt.js";
import { containsForbidden } from "./leakFilter.js";
import type { FactSheet, OracleClient } from "./types.js";

export type { FactSheet, OracleClient } from "./types.js";

/** Gruff line returned when Anthropic is unreachable/errors — play continues. */
export const FALLBACK_LINE =
  "The Keep just grunts — too much ale. Ask again next round.";

/** Safe in-character deflection used when the model leaks or returns nothing. */
export const DEFLECTION_LINE =
  "The Keep smirks, wipes a mug, and gives you nothing useful — nice try, piggy.";

const STRICT_REMINDER = `\n\nSTRICT REMINDER: Your previous reply LEAKED a forbidden word or an identifying detail. Do it again and you lose the game for everyone. Answer once more, still honest and in-character, but with ONLY broad non-identifying truths and ZERO forbidden words.`;

/**
 * Orchestrates the four anti-leak layers around the model call:
 *   1. Fact sheet (forbidden list) — supplied by the caller.
 *   2. System prompt — buildSystemPrompt().
 *   3. Post-generation filter — containsForbidden(); retry ONCE with a stricter
 *      reminder, then fall back to a safe deflection if it still leaks.
 *   4. Scope/injection clamp — baked into the system prompt.
 *
 * Robustness: Anthropic error/timeout -> FALLBACK_LINE. Empty/garbled output ->
 * treated as a deflection. Never throws for a well-formed request.
 */
export async function askKeep(
  question: string,
  factSheet: FactSheet,
  client: OracleClient,
): Promise<string> {
  const system = buildSystemPrompt(factSheet);

  let answer: string;
  try {
    answer = (await client.complete(system, question))?.trim() ?? "";
  } catch {
    return FALLBACK_LINE;
  }

  // Empty/garbled model output -> treat as a deflection.
  if (!answer) return DEFLECTION_LINE;

  if (!containsForbidden(answer, factSheet.forbidden)) {
    return answer;
  }

  // Leak detected — retry ONCE with a stricter reminder appended (Layer 3).
  let retry: string;
  try {
    retry = (await client.complete(system + STRICT_REMINDER, question))?.trim() ?? "";
  } catch {
    return FALLBACK_LINE;
  }

  if (!retry || containsForbidden(retry, factSheet.forbidden)) {
    // Still leaking (or empty) — replace the whole answer with a safe deflection.
    return DEFLECTION_LINE;
  }

  return retry;
}

/**
 * Real Anthropic-backed client. Keeps the API key server-side (env var).
 *
 * NOTE (roundToken future enhancement, per brief §10): instead of the host
 * sending the whole fact sheet on every request, the relay could hold fact
 * sheets keyed by an opaque `roundToken` and accept only `{ roundToken,
 * question }`, so the answer never crosses the wire from the host. We implement
 * the simpler host-sends-factSheet version now; swap the request contract and
 * add a token->factSheet store to adopt the indirection.
 */
export function makeAnthropicClient(apiKey: string): OracleClient {
  const anthropic = new Anthropic({ apiKey });
  return {
    async complete(system: string, question: string): Promise<string> {
      const message = await anthropic.messages.create({
        model: "claude-sonnet-4-6",
        max_tokens: 1000,
        system,
        messages: [{ role: "user", content: question }],
      });
      return message.content
        .filter((block): block is Anthropic.TextBlock => block.type === "text")
        .map((block) => block.text)
        .join("");
    },
  };
}
