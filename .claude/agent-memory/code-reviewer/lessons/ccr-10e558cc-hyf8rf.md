# code-reviewer lessons (branch ccr-10e558cc-hyf8rf)

- A scanner that cuts a file at a heading (`split('## fr')[0]`) while the parser accepts the sections in any order fails open on the other order: a `## fr` before `## en` scanned no English, "Tier 2" passed (promo P5, table/terminology.test.js, 3de3d56). Ask the scanner to read through the parser.
- "Counted in code points, never fewer than the platforms count" is false for X: its weighted count makes an emoji, U+2026 and U+202F weigh 2 and a URL 23; a test asserting 280 emoji fit X asserts the bug (promo P5 calendar.mjs CHANNELS, 3de3d56).
- An "address weighs 23" regex over a closed TLD list undercounts the hosts the same file allows (`youtu.be`, `redd.it`): an "or more" count must treat any `name.tld` as an address (promo P5 calendar.mjs weighted, dbb9600).
- A host taken with `[^\s/]+` and checked with `endsWith('.' + host)` passes `https://evil.example#.x.com/`: parse with `new URL().hostname` (promo P5, dbb9600).
- Moving one checker to "today, Montreal's day" while the writer it reads (brief.mjs intake) keeps UTC refuses the writer's own line each evening: one `today` for both (promo P5, dbb9600).
