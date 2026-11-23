# host-reviewer lessons, sign-in methods P3 (Discord)

- A provider that calls out (DiscordSignIn) folds a bad code, a 429, a 5xx, a timeout and an unreachable provider into one null: the route answers all of them 401 `Account.SignInRejected` and counts `Sign-in refused`, so a provider outage reads as players refused and the page says "sign in again"; ask for a distinct 503 code and line for "could not ask" (P3, 5e85922).
- An unauthenticated route that makes the host call a third party per request (POST /api/account/discord -> oauth2/token) lends the host's egress IP to anyone: Discord's 429s count toward its IP-wide invalid-request ban; ask for an in-process gate before shipping, not "left with #1171" (P3).
- Composition-only knobs survived mutation (measured, 5e85922): `HttpClient { Timeout = SignInProviders.Patience }` and `options.DiscordClientId ?? env` in TableHost, and DiscordSignIn's timeout clause in its catch; TableHostCompositionTests composes Google only (P3).
- The privacy policy (showcase/privacy.mjs, EN and FR) names each sign-in provider and the identifier kept: a new provider needs both languages in the same PR, gated by a variable or not (P3).
