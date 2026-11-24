# code-reviewer lessons (branch fix/account-registry-writes)

- A read-decide-write retry loop whose decide step has store side effects (bind, reserve, unbind) leaks them when a retry decides differently: cleanup keyed on one error (NotFound) misses SessionEnded/NotAGuest/ProviderLinked. Probe with `BeforeNextAccountWrite` changing what the decision reads, not only deleting it (#1217, AccountRegistry.WriteAsync callers, 5ef99b2).
- A new record field "the image before ignores" is dropped by the old image's next whole-record write: read the way-back claim against every unconditional writer of the old release (#1216, AccountDocuments Unbinding, 5ef99b2).
- Third time a "Closes" leaves the plan's "Left:" line naming what it closed (sign-in-methods-plan.md P4, 5ef99b2).
- A "held afresh" fix is only as strong as the release that races it: `ReleaseUsernameAsync` matches the account and re-reads the ETag, so an unlocked `RegisterAsync` that judged the old reservation abandoned deletes the fresh one; check a release made on a judgement compares what was judged (#1227, ReserveAsync, 491d244).
- A rule added in the diff (host.md "the hourly job's write is conditional on what it decided on") broken by the same caller's earlier unconditional writes (`IdleGuests` forgets preferences and activity before the guarded delete) (#1217, 491d244).
