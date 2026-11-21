# code-reviewer lessons (branch claude/scene-paintings-plan-s5-2l1628)

- A layout that drops canonical/hreflang and writes noindex when a lookup misses (`addressed = kind in path`, for the 404) turns any mistyped page kind into a page search engines drop, and only two pages had their canonical asserted: make the exception an explicit kind and throw on an unknown one (scene paintings S5, showcase/layout.mjs:23, f902bcd).
