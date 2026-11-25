# host-reviewer lessons, UX15 (a lesson before an account)

- "The guest's page asks nothing the host refuses a guest" fails at mount: mountLobby's first `void refresh()` (lobby.js:1081) and the page-wide lobby stream (`/api/lobby/negotiate`) leave before /api/me says guest, so `GET /api/lobby` and `/api/lobby/page/open/1` reach GuestRefusalAsync as 403; the spec's mock answers /api/lobby 200 to anyone and filters only four prefixes. Ask for an assertion of every /api path against GuestRoutes (UX15, 532eaca).
- The lobby stream that a guest's 403 marks `refused` comes back only through `signedInChanged` (inbox.setAccount on a new id); check an in-place upgrade still reaches it (UX15).
