# host-reviewer lessons, notifications N1 PR (ADR 0201)

- A per-account document written on behalf of another account (the notice log: match over, void, left) is not stopped by the account's deletion: the operator's void of a finished match (AdminApi.VoidAsync -> NotifyVoidAsync) reads accounts off the record, so a deleted account gets `notices/<id>.json` back with nobody to forget it; check every "written for someone else" store against ForgetAsync and an existence check (N1, same root as O4).
- A wrapper that adds a channel to a send site inside a bare `catch { restore }` (LobbyApi.VacateAsync) must catch its own store failures, or a log blip rolls back a seat already recorded; AccountNotifier does (StoreFails, Json, InvalidData), keep it so (N1).
- A store that skips unknown entries on read and rewrites the whole document drops a later revision's entries on its next write (NoticeDocuments, rollout overlap or a lagging worker image): check forward compatibility of every read-modify-write document (N1).
- The first `dotnet build` of Cli.Tests in a /reviewers copy can report 1 error that a rebuild clears (parallel copy race); rebuild before calling it a finding (N1).
