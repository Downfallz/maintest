# host-reviewer lessons, sign-in methods P2 (providers behind one port)

- Renaming a value `/api/me` answers (`accounts: "google"` -> `"signin"`) breaks the page of the release before, which keys on the old value, whatever extra field is "kept for it" (`googleClientId`); and the new page against an old revision mid-rollout sees neither. Ask that the page accept both and the host keep the old value until a provider the old page cannot draw arrives (P2, 2751f8e).
- A per-request console line on an unauthenticated route (`Sign-in started|refused`) is a free log writer for anyone: ask what bounds it before approving a count kept as log lines (P2).
- A test of "refuses when the host has no X" that passes null for a flag the host also reads from an environment variable (`DOWNFALL_GOOGLE_CLIENT_ID`) fails on a machine that sets it (P2, TableHostCompositionTests).
