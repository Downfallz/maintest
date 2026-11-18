# code-reviewer lessons (branch claude/fix-multiple-issues-wqzx25)

- A widened catch filter fixed on one caller of a shared refusal helper leaves its sibling narrow: `ScenarioTable` restart took JsonException and StoreFails (#846), `PracticeTable` "prepared" (PracticeTable.cs:76) still takes only InvalidDataException/OperationCanceledException; grep every caller of the helper (8f8aa11).
- A console line that says "session {session.MatchId}" uses a different id from every other "Session {id}" line (the table id; MatchId comes from record.MatchId or MatchId.New()): check which id a log word means before accepting a new line (#847, TableApi.NotJson, 8f8aa11).
- Reading a value once through a degrading reader (`TableApi.LastMoveAtAsync`) instead of a throwing one (`composer.LastMoveAsync`) also changes the failure from 500 to a silent null: ask for a test of the degrade path (#775, LobbyApi.AnswerByAsync, 8f8aa11).
