/**
 * Vercel serverless entry — a thin wrapper over the shared request core.
 *
 * Vercel invokes this default export for `POST /api/oracle`. We reuse
 * `processOracleRequest` so deploy and local `src/server.ts` share identical
 * logic. The API key stays server-side via the ANTHROPIC_API_KEY env var
 * (configure it in the Vercel project settings, never in the client build).
 */
import { processOracleRequest } from "../src/server.js";
import { makeAnthropicClient, FALLBACK_LINE } from "../src/oracle.js";

// Minimal structural types so we don't need the @vercel/node dependency.
interface VercelLikeRequest {
  method?: string;
  body?: unknown;
}
interface VercelLikeResponse {
  status(code: number): VercelLikeResponse;
  json(body: unknown): void;
}

export default async function handler(
  req: VercelLikeRequest,
  res: VercelLikeResponse,
): Promise<void> {
  if (req.method !== "POST") {
    res.status(405).json({ error: "Use POST." });
    return;
  }

  const apiKey = process.env.ANTHROPIC_API_KEY;
  if (!apiKey) {
    // Misconfiguration — still keep play going with the gruff fallback.
    res.status(200).json({ answer: FALLBACK_LINE });
    return;
  }

  // Vercel may pre-parse JSON bodies into an object; normalize back to a string
  // so the shared core does the single source-of-truth validation.
  const rawBody =
    typeof req.body === "string" ? req.body : JSON.stringify(req.body ?? {});

  const result = await processOracleRequest(rawBody, makeAnthropicClient(apiKey));
  res.status(result.status).json(result.body);
}
