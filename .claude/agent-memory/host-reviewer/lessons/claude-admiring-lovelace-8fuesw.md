# host-reviewer lessons, sign-in methods P5 (guests, ADR 0207)

- A guard asked of every request before routing (TableServer.GuestRefusalAsync -> AccountAsync) turns an account-store outage into 500 on every cookie-carrying API route, seat routes included: ask that it fail open to the route's own checks or answer 503 with a code (P5).
- A sweep that deletes the account first and its side stores after (IdleGuests: DeleteAsync, then preferences, activity) orphans them for good when a later step fails, as the next pass lists accounts and finds none: delete the side stores first, the account last (P5).
- A cleanup chained after a successful sign-in (ProviderAsync -> ForgetAsync of the guest) that throws loses the Set-Cookie already built: catch it, log, and still answer the sign-in (P5).
