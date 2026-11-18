# code-reviewer lessons (branch fix/recap-count-and-load-back)

- Rewording what a page says of a decided concept ("who leads" -> a count, #757): grep `docs/domain/glossary.md` for the concept's row too; the plan, the README and the terminology table were updated, the glossary's Recap row (glossary.md:31) still said "who leads" (4295baa).
- A load-back test (#758) is proven by mutation, not by its count: commenting out `void loadBack()` in live.ts turned "5 new" into "2 new", so the spec kills it; the mock serves the full feed only to a read with no `since` after the first, which is what `connection.history()` sends (transport.js query) (4295baa).
