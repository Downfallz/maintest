# code-reviewer lessons (branch claude-battle-fx-issues-0jp69q)

- A refactor sold as "no longer relies on the engine's call order" (burst's `drawn`) can move the dependency to another call (the emit zone first), and its spec passes on the old code: read the installed `Particle.fire` order before accepting the claim, and ask for the test to fail on `origin/main` or be called a guard (#936, 6695b12).
- A bench getter that grows a field whose only assertion is its empty value tests nothing: ask for a spec that reaches the non-default value (`focusQuiet`, #937, 6695b12; seen before in #812 and #918).
