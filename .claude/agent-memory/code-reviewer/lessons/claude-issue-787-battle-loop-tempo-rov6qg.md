# code-reviewer lessons (branch claude/issue-787-battle-loop-tempo-rov6qg)

- A "newest seat view" guard keyed on (feedNext, waitingAsked) drops the held-answer gap view: a held answer adds no trace entry, so the gap has the same feedNext with waitingAsked null and the stale asking stays `latest`. Order bodies by response arrival instead (#787, studio/browser/seat-view.js:7, fe26bd0).
- The battle page sends a team's tempos one asking at a time (table/battle-tempo.js pump), so one journey step covers several Speed askings: `waitingAsked !== view.waitingAsked` holds already on the 2nd creature's view, before the round leaves Speed (#787, battle.loop.js:291).
