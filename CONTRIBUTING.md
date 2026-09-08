# Contributing

This is a personal project, but it is run like a professional one on purpose.

## Setup

- .NET SDK matching `global.json` (10.0.x). `dotnet --version` should print a 10.0 version.
- Any editor with EditorConfig support. `.devcontainer/` gives you a ready environment in VS Code or Codespaces.

## Workflow

1. Branch from `main`: `feature/<short-name>`, `fix/<short-name>`, `docs/<short-name>`.
2. Keep pull requests small and focused. One decision, one feature, or one fix.
3. Before pushing:

   ```bash
   dotnet build
   dotnet test
   dotnet format --verify-no-changes
   ```

4. Open a PR using the template. CI must be green.

## Decisions

Anything that changes architecture, a dependency, a convention, or a game rule in a way that is hard to reverse
gets an ADR in `docs/adr/`. Copy `docs/adr/0000-template.md`, take the next number, keep it short.

## Commit messages

Imperative subject under 72 characters, blank line, then the why. Reference issues with `#123`.

## AI-assisted changes

AI-generated code follows the same rules as any other code: it is reviewed, tested, and formatted.
`AGENTS.md` is the contract with the agents. If an agent keeps doing something wrong, fix the instructions there.
