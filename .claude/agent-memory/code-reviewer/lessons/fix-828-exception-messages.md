# code-reviewer lessons (branch fix/828-exception-messages)

- AdminApi has a `Said` sink (init property, defaults to Console.WriteLine) that its other lines use; a new `Console.WriteLine` in AdminApi bypasses it and drops the operator's name: grep the class for `Said` before accepting a console line there (#828, AdminApi.cs:367, 0ae3a5a).
- Replacing an exception's text with a fixed sentence that names one cause is wrong when the catch filter is wider than that cause: AdminApi's compose catch takes IOException, which `TableRegistry.StoreFails` also matches, so a store outage now reads "a weights or policy file is missing"; split on StoreFails first as LobbyApi does (#828, 0ae3a5a).
- Second sweep of exception text (#786, then #828): this time the `\.Message` grep over src/DownfallArena.Cli came back clean for table answers; the rule now lives in `.claude/rules/host.md` (#828).
