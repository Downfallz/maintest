# code-reviewer lessons (branch claude/stage-ux-craft-plan-6c11np)

- A route dependency added as an optional constructor parameter (`MutationArt? art = null`) lets the host's wiring drop it with no compile error and no failing test when the only test injects it by hand: make it required and test the field through the host's services (#812 again, UX10, StatsApi.cs:20, 0bbfa8a).
