# code-reviewer lessons (branch ccr-9dcf39ea-na7ock)

- A UI wait is a causal wait for a `writes` read only if the UI change comes from the host's answer: the reactions menu closes and draws `#reaction-mine` before the POST (battle/src/reactions.ts:19, table/battle-reactions.js:115), so battle-reactions.spec.js:68 reads `writes` with nothing that orders it after the request (#816/#831 polling pass, 7d2c2cc).
- Every battle spec that pauses the clock does it in `mount` (pauseAt), so `expect.poll` never advances page timers: an acknowledgement that rides a page timer fails at the 5 s poll timeout, as the bare read failed at once; it cannot be masked (7d2c2cc).
- In this sandbox the changed battle set at --workers=4 on two projects fails ~4 untouched tests, and --workers=6 fails 18 on 30 s timeouts; battle-order.spec.js:215 (line 242 layout poll) fails alone on desktop on origin/main too. Use workers=4, one project, and a main copy next to it before blaming a change (7d2c2cc).
