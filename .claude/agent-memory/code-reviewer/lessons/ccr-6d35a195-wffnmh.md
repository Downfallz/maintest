# code-reviewer lessons (branch ccr-6d35a195-wffnmh)
- A "asks the host nothing it refuses a guest" spec filtered `state.requested` by a regex of a few routes; printing the list showed /api/lobby, /api/lobby/page/open/1, /api/lobby/negotiate, /api/lobby/events and /api/feedback, all 403 to a guest: hold such a claim to the allow list (GuestRoutes), not a regex (UX15, 532eaca).
- `access-known.json` lists must be sorted with `[...new Set].sort()`: a `~ ` line sorts after every digit, so a hand-placed one turns `ux-access.test.js` red (UX15, 532eaca).
