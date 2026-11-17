# code-reviewer lessons (branch fix/756-table-host-sink)

- `TutorialPlay` goes to the page as it is (`ScenariosApi.Opened` -> `$.practice.Tutorial` through `StudioResponse.OfJson`): a new public member on it (an `Action<string> Said`) makes every tutorial open answer 500 with NotSupportedException; 13 ScenariosApiTests failed on 3804ce4, and be868a5 added `[JsonIgnore]`. Before adding a public property to a host type, grep for it being sent whole through `OfJson` (#756).
- Turning a console swap into a host sink narrows a "never said on the console" assertion (#624's explanation check) to the host's own lines: the composer and OperatorNotes on the same path still write to Console unseen. Say so when the test guards a leak (#756, OperatorVoidTests, PlayersApiTests).
- Twelfth time: the branch moved mid-review (3804ce4 -> be868a5); gate re-run on be868a5. Belongs in `.claude/agents/code-reviewer.md`.
