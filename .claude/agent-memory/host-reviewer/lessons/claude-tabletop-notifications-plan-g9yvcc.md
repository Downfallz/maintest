# host-reviewer lessons, notifications N2 PR (ADR 0201)

- AccountNoticeLog drops entries past 90 days only when it reads or writes (`Kept`); nothing sweeps `notices/`, and infra/main.bicep has no lifecycle rule: an idle account's log keeps expired entries (other players' old names included) at rest until its next notice or its deletion. Check every retention sentence against what deletes at rest, not what is listed (N2 privacy text, same shape as R3's mute).
- A `Changes` sink set as a property in TableHost (`accounts?.Changes = lobbyChanges`, N2) and tested by setting it on the api directly is the #812/UX10 shape again: dropping the host line stays green (N2).
- The bell's refresh (inbox.js) runs on every account change and stream open of every page: each read it adds is one store read per page per change, not throttled by `fresh` (N2: GET /api/account/notices).
