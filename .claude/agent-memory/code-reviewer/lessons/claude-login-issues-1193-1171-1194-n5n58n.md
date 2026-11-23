# code-reviewer lessons (branch claude/login-issues-1193-1171-1194-n5n58n)
- A count held in memory and said "on the next request" is lost whenever the table idles to zero (`minReplicas: 0` in infra/main.bicep): ask for a flush when the host stops, and a test of it (#1171, SignInCounts.Due, 73c1217).
- A pull request that closes the plan's "Left:" items must rewrite those lines too: sign-in-methods-plan.md kept "Left: ... (#1171)" and P3's "Left: the Discord journey ... per network address" after closing #1171, #1193, #1194 (73c1217).
