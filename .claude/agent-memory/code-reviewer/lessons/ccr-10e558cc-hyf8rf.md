# code-reviewer lessons (branch ccr-10e558cc-hyf8rf)

- A scanner that cuts a file at a heading (`split('## fr')[0]`) while the parser accepts the sections in any order fails open on the other order: a `## fr` before `## en` scanned no English, "Tier 2" passed (promo P5, table/terminology.test.js, 3de3d56). Ask the scanner to read through the parser.
- "Counted in code points, never fewer than the platforms count" is false for X: its weighted count makes an emoji, U+2026 and U+202F weigh 2 and a URL 23; a test asserting 280 emoji fit X asserts the bug (promo P5 calendar.mjs CHANNELS, 3de3d56).
