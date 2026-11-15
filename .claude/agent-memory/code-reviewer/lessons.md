# code-reviewer lessons (one line each, with the PR)

- Build the battle bundle BEFORE `dotnet build`: Cli.Tests copies table/ at build time, so a bundle built after gives 3 false TableFilesTests failures (PR 578).
- Give the review worktree a unique name in the scratchpad: other reviewers share it, and one deleted a `pr578` worktree mid-test, which caused 546 false IO failures (PR 578).
- A new command (`node showcase/build.mjs`, env `GAME_ORIGIN`) shipped without a `docs/reference/commands.md` heading; check the docs the Definition of done lists for every new script (PR 578).
