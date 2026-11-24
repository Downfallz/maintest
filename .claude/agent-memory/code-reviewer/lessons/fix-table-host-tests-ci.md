# code-reviewer lessons (branch fix/table-host-tests-ci)

- A flake fix that names its cause (#1187: two registries over one host's services share one `InMemoryMatchRepository`) must show the old test failing: 11 runs of the old line, 6 of them under 8 busy CPUs, stayed green, so the cause is a guess. Ask for the assertion to print the reason (`Found.Unavailable`) so the next failure says why (c19c933).
- An Azurite test shares account-wide queue names (`agent-turns`, `notices`) with every run on that emulator: a `PeekAsync(32)` check plus cleanup outside `finally` goes red for good once 32 leftovers pile up. Ask for a unique body and cleanup in `finally` (#1181, c19c933).
- Cross-check a CI-numbers paragraph with `gh api repos/<o>/<r>/actions/runs?branch=main&created=<a>..<b>`: #1096's "41 runs, 12 cancelled, green again from 22:01" missed a red main run of the next day (c19c933).
