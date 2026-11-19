# code-reviewer lessons (branch claude/tabletop-tutorial-followups-plan-j0cxy0)

- A re-cut tutorial's lines name spells: read them against the checkpoint's `knownSpells` and each spell's `criticalChance` in `data/Spells/`, not against the recorded answers ("Strike and Brace never roll" left out Focus, which creature 1 also knows) (T2, #727, 8b4b67a).
- `TutorialPlay` is done only once its last step has shown and a later round is asked (TutorialPlay.cs:222): a last step at a creature that can be defeated before it acts plays the tutorial on to the match's end; check the step's creature survives what acts before it (T2, 8b4b67a).
- A dated note added to a plan's "Built" section between two bullets splits the list and leaves the next bullet reading as part of the note: put it after the list (onboarding-plan.md, T2, 8b4b67a).
