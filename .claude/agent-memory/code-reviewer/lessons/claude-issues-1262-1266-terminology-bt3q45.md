# code-reviewer lessons (branch claude/issues-1262-1266-terminology-bt3q45)

- A new test reused its class's `Read` helper, which asserts a 2xx, on a 404 answer: red on every run, and the author's gate did not catch it. Run the Cli tests on the copy before trusting "the gate passed" (#1266, LobbyApiTests.A_seat_on_a_host_without_push_keys..., 778fd3a).
- A wording scanner scoped to the files the issue listed misses the same sentences in a sibling (`table.js` testNotices) and the shared fallback the module calls (`account.js` accountRequest's "The host answered N."): reverting either stays green (#1266, 778fd3a).
- A `error.message || 'Could not reach ...'` fallback never shows on a network failure: fetch throws a TypeError whose message is "Failed to fetch". Read a "could not reach" sentence against what the catch actually receives (#1266, devices.js, 778fd3a).
