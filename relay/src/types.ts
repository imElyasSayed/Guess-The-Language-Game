/**
 * Shared types for the oracle relay.
 *
 * The `FactSheet` is the shape produced by the prep pipeline
 * (`prep/` -> per-language `forbidden` fact sheets). The trusted host holds the
 * round's answer and sends the fact sheet + the player's question to the relay.
 */

export interface FactSheet {
  /** e.g. "Spanish" */
  language: string;
  /** origin country — the answer, e.g. "Mexico" */
  country: string;
  /** e.g. "North America" */
  continent: string;
  /** normalized lowercase words: language, country, capital, demonym, currency, landmarks, proper nouns */
  forbidden: string[];
}

/**
 * Injectable seam over the Anthropic Messages API.
 *
 * `askKeep` depends only on this interface, so tests can pass a fake client and
 * never touch the network. `makeAnthropicClient` (in `oracle.ts`) is the real
 * implementation.
 */
export interface OracleClient {
  /** Send one message and return the model's text output (may be empty). */
  complete(system: string, question: string): Promise<string>;
}
