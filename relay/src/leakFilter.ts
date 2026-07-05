/**
 * Layer 3 — post-generation leak filter (pure, unit-tested).
 *
 * Scans model output against the forbidden list:
 *   - case-insensitive
 *   - accent-insensitive (NFKD decompose + strip combining marks)
 *   - WHOLE-WORD match — "spain" must NOT trip on "explain".
 */

/**
 * Fold a string to a comparable form: NFKD-decompose, strip diacritics, lowercase.
 * e.g. "Español" -> "espanol", "MÉXICO" -> "mexico".
 */
export function normalize(text: string): string {
  return text
    .normalize("NFKD")
    .replace(/[̀-ͯ]/g, "") // strip combining diacritical marks
    .toLowerCase();
}

function escapeRegExp(literal: string): string {
  return literal.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

/**
 * True if `text` contains any forbidden term as a whole word (Unicode-aware
 * boundaries). Multi-word forbidden terms (e.g. "world cup") are matched as a
 * contiguous phrase. Empty terms are ignored.
 */
export function containsForbidden(text: string, forbidden: string[]): boolean {
  const haystack = normalize(text);
  for (const raw of forbidden) {
    const term = normalize(raw).trim();
    if (!term) continue;
    // Whole-word: not preceded/followed by a letter or number. Using \p{L}\p{N}
    // (with the `u` flag) so non-ASCII scripts are handled and substrings like
    // "spain" inside "explain" do not match.
    const re = new RegExp(
      `(?<![\\p{L}\\p{N}])${escapeRegExp(term)}(?![\\p{L}\\p{N}])`,
      "u",
    );
    if (re.test(haystack)) return true;
  }
  return false;
}
