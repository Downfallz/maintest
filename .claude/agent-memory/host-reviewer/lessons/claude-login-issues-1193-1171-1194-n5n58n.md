# host-reviewer lessons, #1193/#1171/#1194 PR (sign-in counts and per-address limit)

- Work flushed "on the next request" (SignInCounts.Due from TableServer.AnswerAsync) loses the last window whenever the API scales to zero (infra/main.bicep minReplicas 0; the probes are TCP and never reach AnswerAsync): ask for a flush when RunAsync stops (#1193 PR, 73c1217).
- A per-address limit that counts successes too, on every provider, turns one venue's shared IPv4 (a playtest night, a campus NAT) into five sign-ins a minute for the room; ask whether only failed proofs should spend the count (#1193 PR).
- RequestAddress counts IPv6 by /64: a holder of a /48 or /56 has 256-65536 keys, so a per-address limit is per-attacker only for IPv4 (#1193 PR).
- A slot taken before an awaited provider call and given back after it is held for the call's length: "successes are given back" still turns away the sixth of a room that signs in at once, with a Retry-After of a minute (#1193 PR, f37708d).
- The table host stops on SIGINT only (Console.CancelKeyPress); TableWorker already registers SIGTERM with PosixSignalRegistration. Ask for that over an AppDomain.ProcessExit handler, which no test reaches (#1193 PR, f37708d).
