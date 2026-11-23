# host-reviewer lessons, #971/#1133/#1132 PR (fix/lobby-push-security)

- Lib.Net.Http.WebPush 3.3.1 defaults AutoRetryAfter=true, MaxRetriesAfter=0 (unlimited): a 429 with Retry-After makes one RequestPushMessageDeliveryAsync wait and retry, outside HttpClient.Timeout; a "send gives up after N seconds" needs AutoRetryAfter=false or a linked CancellationTokenSource (#1132 PR, measured).
- An allowlist checked only at subscribe leaves every endpoint stored before the deploy (devices/<id>.json, seat.Push in table records) sent to: check it at send too (#1132 PR).
- HeldNotices.AfterAsync gives held notices only when the work returns: a release over many tables that throws on a later one drops the notices of tables already vacated, which a retry never re-sends (#1133 PR).
