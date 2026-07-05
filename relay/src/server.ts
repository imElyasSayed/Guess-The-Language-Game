import http from "node:http";
import { askKeep } from "./oracle.js";
import type { FactSheet, OracleClient } from "./types.js";

/**
 * Framework-light HTTP layer.
 *
 * `POST /oracle`  body: { question: string, factSheet: FactSheet }
 *                 -> 200 { answer: string }  (always 200 for a well-formed
 *                    request, even if Anthropic fails — see oracle fallback)
 *                 -> 400 { error } for an invalid body
 * `GET  /health`  -> 200 { ok: true }
 *
 * The client is injectable so the whole handler can be exercised in-process
 * with a fake client (no network).
 */

export interface OracleResult {
  status: number;
  body: unknown;
}

function isFactSheet(v: unknown): v is FactSheet {
  if (typeof v !== "object" || v === null) return false;
  const f = v as Record<string, unknown>;
  return (
    typeof f.language === "string" &&
    typeof f.country === "string" &&
    typeof f.continent === "string" &&
    Array.isArray(f.forbidden) &&
    f.forbidden.every((w) => typeof w === "string")
  );
}

/**
 * Pure request core: parse + validate a raw JSON body and run the oracle.
 * Returns a status + JSON body. Directly unit-testable without a socket.
 */
export async function processOracleRequest(
  rawBody: string,
  client: OracleClient,
): Promise<OracleResult> {
  let parsed: unknown;
  try {
    parsed = JSON.parse(rawBody);
  } catch {
    return { status: 400, body: { error: "Body must be valid JSON." } };
  }

  if (typeof parsed !== "object" || parsed === null) {
    return { status: 400, body: { error: "Body must be a JSON object." } };
  }

  const { question, factSheet } = parsed as Record<string, unknown>;

  if (typeof question !== "string" || question.trim() === "") {
    return { status: 400, body: { error: "`question` must be a non-empty string." } };
  }
  if (!isFactSheet(factSheet)) {
    return {
      status: 400,
      body: {
        error:
          "`factSheet` must be { language, country, continent: string, forbidden: string[] }.",
      },
    };
  }

  const answer = await askKeep(question, factSheet, client);
  return { status: 200, body: { answer } };
}

function readBody(req: http.IncomingMessage): Promise<string> {
  return new Promise((resolve, reject) => {
    const chunks: Buffer[] = [];
    let size = 0;
    const LIMIT = 64 * 1024; // 64 KB is plenty for a question + fact sheet.
    req.on("data", (c: Buffer) => {
      size += c.length;
      if (size > LIMIT) {
        reject(new Error("payload too large"));
        req.destroy();
        return;
      }
      chunks.push(c);
    });
    req.on("end", () => resolve(Buffer.concat(chunks).toString("utf8")));
    req.on("error", reject);
  });
}

function sendJson(res: http.ServerResponse, status: number, body: unknown): void {
  const payload = JSON.stringify(body);
  res.writeHead(status, {
    "content-type": "application/json",
    "content-length": Buffer.byteLength(payload),
  });
  res.end(payload);
}

/** Build a request listener bound to a given oracle client. */
export function createRequestListener(
  client: OracleClient,
): http.RequestListener {
  return async (req, res) => {
    try {
      if (req.method === "GET" && req.url === "/health") {
        return sendJson(res, 200, { ok: true });
      }
      if (req.method === "POST" && req.url === "/oracle") {
        const raw = await readBody(req);
        const result = await processOracleRequest(raw, client);
        return sendJson(res, result.status, result.body);
      }
      return sendJson(res, 404, { error: "Not found." });
    } catch {
      // Body read / unexpected transport error. Oracle failures never reach
      // here (askKeep never throws), so this is a request-level 400.
      return sendJson(res, 400, { error: "Bad request." });
    }
  };
}

/** Create an http.Server wired to the given client (testable in-process). */
export function createServer(client: OracleClient): http.Server {
  return http.createServer(createRequestListener(client));
}
