# host-reviewer lessons, sign-in methods P4 (ways to sign in)

- A registry write that says "doing it again finishes the job" (UnlinkAsync: save, then unbind) is reachable only if the route can name the half-done thing again: DELETE /identities/<provider> takes the identity from the account's own listing, which no longer holds it after the save, so a failed unbind is never retried and the `by-subject` blob outlives DeleteAsync (P4, c0f8160). Check the route's pre-check against the registry's recovery path.
- A route that raises the generation and hands the new cookie only on success (RemoveWayAsync) signs its own browser out on any failure after the save: the retry is 401, not a finished job. Ask for the cookie on every answer after the save (P4).
- A route documented as "forgets its devices" tested with `Devices` unset: deleting the call stays green (P4; same shape as #1109/#1117 wiring).
