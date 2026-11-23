# code-reviewer lessons (branch fix/lobby-push-security)

- `HttpClient.Timeout` bounds one request, not a Lib.Net.Http.WebPush send: `PushServiceClient.AutoRetryAfter` defaults true with `MaxRetriesAfter` 0 (unlimited), so a 429 with Retry-After waits past any timeout. Probed by reflection on 3.3.1 (#1132, WebPushSender.SendFor, 64f8171).
- An allowlist checked only where a value is written leaves every value already stored before the deploy: check at the use (the send) too, or sweep (#1132, OnKnownService on PUT only, 64f8171).
