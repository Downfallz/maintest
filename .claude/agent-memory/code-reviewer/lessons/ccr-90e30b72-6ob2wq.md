# code-reviewer lessons (branch ccr-90e30b72-6ob2wq)

- A page write sent with no body (`accountRequest(path, undefined, undefined, 'DELETE')`) carries no Content-Type, and the table's fence (`HttpHost.CrossSite`) answers 415 to every non-GET without `application/json`; Cli tests call `AccountApi.HandleAsync` past the fence, so they stay green. Grep the new page calls for `undefined, undefined, '` (P4, table/ways.js:103, c89ca77).
- Google Identity Services keeps one `initialize` config per page, the last one called: a second module that initializes with its own callback (Settings' "Add Google") takes over the sign-in button's, or loses its own to it. Ask for one owner of `initialize` (P4, ways.js:139 vs account.js:230, c89ca77).
