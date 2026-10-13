# 0110. A rule set may declare spells at activation

Date: 2026-10-11
Status: Accepted, its default changed by [0112](0112-spells-are-chosen-at-the-slot-by-default.md)

## Context

Today every creature on the timeline declares a hidden Intent before combat, and at its slot its owner only
chooses targets (ADR 0083, 0107). A review brought to the owner proposes choosing the spell and its targets
together at the slot, which would remove `IntentSelection`, the Intent dial or concealed hand, and most of
the reason for six copies of each spell card. It would also make combat a game of perfect information but for the d20:
the journal measured that the Intent is the only combat decision that guesses (2026-09-16), and every tuning
pass, digest and searched weights file was measured with it. Neither reading can be settled by argument, and
the core is frozen until a playtest names a problem (content review, 2026-10-07). The owner lifts the freeze
for this one experiment, provided both rules can be played and the switch can be undone.

## Decision

We will add a combat declaration mode to `RuleSet`: `HiddenIntent`, today's rule and the default, and
`AtActivation`. In `AtActivation`, `IntentSelection` passes through without asking anyone, as `TieOrder`
does in a round with no tie. At each slot the owner submits one action, a spell the creature knows and can
pay for now and its targets on the board as it stands, and it resolves on confirmation as ADR 0083 rules.
Spell and targets become public together (ADR 0070). A creature that has no legal cast when its slot comes
up, dead, stunned or with nothing it can pay for on a legal target, is skipped without its owner being
asked. With today's content only death and stun can do that, because every creature starts with Focus
(`wait`, free, on itself). Nothing is declared that cannot be paid, so ADR 0107 holds for `HiddenIntent` only. The mode is
part of the rule set's stamp, so runs of the two modes never compare as the same rules, and `benchmark`
refuses `AtActivation`, as it refuses `--format`. The work, the measurements and the stop rule are in
[free-activation-plan.md](../domain/free-activation-plan.md).

## Consequences

- Good: both rules play on the same engine, the same content and the same table, so the comparison is fair
  and dropping the losing one is one pull request.
- Good: the resolution path (`ResolutionRules`, `CombatExecution`, `Advance`, `TargetingRules`) already reads
  an action and never an Intent, so it does not change.
- Bad: two modes to keep green in the domain, the agents, the encoders, the host, the page and the viewer
  until one is dropped. Every wrapping agent must forward the combined decision explicitly.
- Bad: in `AtActivation` the lookahead and the minimax need a combined search from the activation cursor;
  their guess of an enemy's spell becomes exact at its slot rather than wrong on purpose.
- Bad: `AtActivation` is measured against a catalogue tuned for `HiddenIntent`. Its balance readings say how
  much re-tuning adoption would cost, not that it is worse.
- Neutral: the default does not move, so the benchmark digests, recorded sessions, policies and weights files
  are unchanged. The feature schema does not move: the observation encodes no Intent.

## Alternatives considered

- Replace the Intent outright: no way back if the table misses the bluff, and every reading loses its
  baseline.
- Remove the `IntentSelection` sub-phase in the new mode: breaks the flow table and the observation's
  sub-phase index for no gain.
- A hybrid, some creatures committing and others not: a second rule inside the rule, which would blur the
  experiment. The enum leaves room for it.
- Decide from the bots alone: they do not bluff, so they cannot say whether the hidden Intent is worth its
  components.

## Follow-up

- `RuleSet`, `Match`, `Round`, `ActionRules`, `CombatAction`, a skipped-slot event and `SeatVisibility`.
- `PlayerOptionsKind`, the options, decision and check, `MatchDriver`, `IPlayerAgent` and every agent,
  `RoundRollout`, `IntentCounter`, `ActionEncoder`, `CandidateTerms`, `RecordingAgent`, `RuleSetStamp`.
- `RuleSetFile`, `TableDecisionBody`, `HumanSeat`, `DecisionJournal`, `ConsoleAgent`, the `benchmark` guard,
  the table page, the viewer.
- `docs/domain/game-rules.md` and the glossary when the mode is implemented; the rulebook, player aid,
  components and Automa only if it is adopted, by a second ADR.
