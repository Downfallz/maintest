# host-reviewer lessons, #1162/#1153/#1159 PR (composition split)

- A long positional parameter list turned into a record of `init` properties (TableTend.TendAsync -> `Chores`) makes a dropped middle argument silent instead of a type error: mutation-probe every property at each construction site; the panel pass's Composer/Suspended/Feedback/FeedbackIndex (TableHost.JobsTabAsync) survived the whole Table namespace (#1159 PR, c69c7cd).
- The relay-only setters noted unpinned in #1109 (`composer.Notifier?.Changes` in TableWorker/TableTend) and `friends?.Changes` in TableHost are pinned since c69c7cd through a relay seam (`Over.Relay`, `ComposeAsync(..., relay)`) and fail when deleted (measured).
- The session scratchpad is shared with reviewers running in parallel: a directory named `mut` was rewritten under my probe (`// mutB` lines). Name a probe copy after the reviewer and sha (`hostrev-<sha>`) (#1159 PR).
