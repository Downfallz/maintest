# code-reviewer lessons (branch ccr-8eb614e4-o3u4lm)

- A refactor that turns positional arguments into an init-only parts record (`TableTend.Chores`) makes each part an optional line that can be dropped without a compile error: probe every initialiser, not only the one a test names. Here the panel's pass kept only `Notices` pinned; Composer, Suspended, Feedback, FeedbackIndex and the job's `Clock` were deletable with Cli.Tests green (#1159, c69c7cd).
- A mutant whose build fails (IDE0060 on a parameter the deletion left unused) runs the previous binaries and reports their results: check "Build succeeded" before reading a mutant's test run, and use `_ = param;` to keep the mutant compiling (c69c7cd).
- Moving `FromEnvironment()` out of a ComposeAsync into RunAsync for a test seam leaves the production line in a method no test reaches: say so, it is the price of the seam (#1153, c69c7cd).
