# host-reviewer lessons, #971/#1133/#1132 PR (fix/lobby-push-security)

- Lib.Net.Http.WebPush 3.3.1 defaults AutoRetryAfter=true, MaxRetriesAfter=0 (unlimited): a 429 with Retry-After makes one RequestPushMessageDeliveryAsync wait and retry, outside HttpClient.Timeout; a "send gives up after N seconds" needs AutoRetryAfter=false or a linked CancellationTokenSource (#1132 PR, measured).
- An allowlist checked only at subscribe leaves every endpoint stored before the deploy (devices/<id>.json, seat.Push in table records) sent to: check it at send too (#1132 PR).
- HeldNotices.AfterAsync gives held notices only when the work returns: a release over many tables that throws on a later one drops the notices of tables already vacated, which a retry never re-sends (#1133 PR).
- Round 2: a timeout put in two places (AutoRetryAfter=false and a linked CancelAfter) is pinned by one test only if each is probed alone: removing AutoRetryAfter=false stays green because the CTS covers the 429 too; ask for `Received.Count == 1` (#1132 PR, measured).
- A route test named after a clause ("in another format") can be short-circuited by an earlier one (`ranked:false` sets Casual first): removing `Format != ThreeOnThree` from LobbyApi.Ranked stays green; the clause is reachable only from a table recorded before ADR 0171's refusal (#971 PR, measured).
