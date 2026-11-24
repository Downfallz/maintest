# host-reviewer lessons, sign-in methods P6 (Microsoft, d9fc6cc)

- A provider error word read as "the app is refused" (CodeExchangeSignIn.AppRefused: invalid_client, unauthorized_client) turns a 4xx into SignInUnavailableException, which VouchedAsync settles as Unchecked and so costs the address nothing: whoever can make the provider answer that word spends the provider's 30-a-minute budget for free; ask that an app-refused answer be settled as Refused (P6).
- `Returning` (Discord's since P3, shared since P6) checks scheme and path but not the host: any `https://<anything>/signin/<provider>` is forwarded to the provider; the provider's redirect check is the only guard (P6).
- `SendAsync` reads a non-success body before `Unsuccessful`: a 429 whose body stalls past the patience ends as a timeout and never sets the hold-back (P6).
- Probe measured at d9fc6cc: `loopback` in SignInProviders' Microsoft case replaced by `false` fails 2 Table tests: pinned (P6).
