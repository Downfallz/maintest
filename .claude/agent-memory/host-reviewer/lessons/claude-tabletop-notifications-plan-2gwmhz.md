# host-reviewer lessons, notifications N4 PR (the offer)

- A practice or tutorial seat token lives in ScenarioPractices, not TableRegistry: `PUT /api/lobby/push` (LobbyApi.SubscribeAsync, registry.ByTokenAsync) answers it 403, and battle-services' subscribe then calls `made.unsubscribe()` on the browser's one subscription, the account's devices' too. Any page path that pushes from a practice seat (N4's lesson-1 offer) silently turns the device off; check `practice` reaches noticeButton in battle-services (N4).
- A page spec that mocks `/api/lobby/push` for every token hides a host refusal by seat kind: ask which moments of a new page flow reach the real host (N4: the lesson-end offer had no test at all).
