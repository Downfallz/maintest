# code-reviewer lessons (branch fix/account-registry-writes)

- A read-decide-write retry loop whose decide step has store side effects (bind, reserve, unbind) leaks them when a retry decides differently: cleanup keyed on one error (NotFound) misses SessionEnded/NotAGuest/ProviderLinked. Probe with `BeforeNextAccountWrite` changing what the decision reads, not only deleting it (#1217, AccountRegistry.WriteAsync callers, 5ef99b2).
- A new record field "the image before ignores" is dropped by the old image's next whole-record write: read the way-back claim against every unconditional writer of the old release (#1216, AccountDocuments Unbinding, 5ef99b2).
- Third time a "Closes" leaves the plan's "Left:" line naming what it closed (sign-in-methods-plan.md P4, 5ef99b2).
