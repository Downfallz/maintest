# code-reviewer lessons (branch ccr-e2ea37c5-n3ki3o)

- A refusal test fed a fixture that an earlier guard already refuses ("not redacted" fed a generated file with no `session`, refused by the session check first) passes with the named check disabled: probe by disabling it (R6 import, ScenarioImportTests, b649f64).
- Third time (R4 recap, #739, R6): a field added to a route's JSON (`bug` in GET /api/admin/scenarios) tested only in the page's pure mapper, whose fixture writes the field by hand; nulling it at the host left all 1514 Cli tests green (R6, ScenarioAdminApi.cs:69, b649f64). Belongs in `.claude/rules/host.md`.
- `dotnet format` IMPORTS fires on a test file with a using out of order (Infrastructure before Domain); `dotnet build` and `dotnet test` stay green, only the format step catches it: always run it (R6, ExampleBugTests.cs:1, b649f64).
