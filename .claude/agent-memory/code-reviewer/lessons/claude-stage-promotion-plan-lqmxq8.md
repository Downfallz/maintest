# code-reviewer lessons (branch claude-stage-promotion-plan-lqmxq8)

- A lookup with `find` over an append-only record (`promo/media.json`, a re-filmed shot) returns the oldest line: ask for `findLast` or a refusal of two lines, and a fixture with two (promo P7 checklist.mjs `where`, 9deb081).
- A status-moving command (`--publish`) that refuses only the end state lets a post skip the middle one (draft straight to published): probe every from-state (promo P7, 9deb081).
- A script that rewrites a file by split/join on `\n` drops CRLF and a BOM; harmless here under `eol=lf`, but check `.gitattributes` before calling it a bug (promo P7, 9deb081).
