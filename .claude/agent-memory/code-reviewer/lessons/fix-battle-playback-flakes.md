# code-reviewer lessons (branch fix/battle-playback-flakes)

- Reproduce a flake against the old spec next to the new one in one run: copy `git show origin/main:<spec>` in under another name, then `--repeat-each=4 --workers=4`. #749's old journey failed 3 of 4 at the 30 s timeout with CI's exact `Received: ""`, and the new one passed 4 of 4 in 10-14 s (#749, 3c1b88b).
- A browser fixture that says "the change arrived" because a route answered the published object counts every caller of that route: `/api/seat/player1` also serves the acknowledgement (`?shown=`) and history (`seat(0)`) reads, which the board never applies. Key it on the read that feeds `onView` (#768, battle-upkeep.spec.js mount).
- The battle page hides `#loading` in `choose()`, before `connection.start`'s first read: "loading hidden" never meant "first board arrived". `#connection` reading LIVE does (#768, live.ts:805).
- `page.clock.fastForward(n)` moves every due timer to the target and runs each once, the rAF too: one frame instead of n/16. A timer scheduled inside it starts from the target, and a later fastForward that lands exactly on its due time still fires it (`callAt <= to`) (#749).
