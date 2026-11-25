# code-reviewer lessons (branch fix/host-followups-1226-1259)

- A sweep of "what a removed account left" that deletes by listing difference needs its re-read guard pinned: `IdleGuests`' `FindAsync(left) is null` replaced by `true` kept Cli.Tests green, so an account made after the listing loses its settings with no test noticing (#1253, fe995f6).
- An env var read by two processes (`DOWNFALL_TURNS_QUEUE`, host and table-worker) tested in one only: the host's `OpenQueuesAsync` could ignore it with the suite green, which splits the bots' turns from their worker. Ask for each reader's test (#1259, fe995f6).
