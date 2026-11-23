# host-reviewer lessons, sign-in methods P1 (ADR 0204)

- Two writes in opposite orders (LinkAsync binds then saves, UnlinkAsync saves then unbinds) leave the same half state, a `by-subject` binding the account does not list; the registry finishes whichever call comes next, and sign-in (`FindAsync(identity)`) honours it meanwhile. Ask for one predicate, "a binding counts only when its account lists the identity", used by sign-in, register and link (P1, a185a2d).
- An index entry no listing names (an orphan `by-subject` blob after an old image rewrote the account) is never reached by `DeleteAsync`, which unbinds only what the account lists: the provider identifier stays at rest past "until the account is deleted" (showcase/privacy.mjs:33) (P1, a185a2d).
