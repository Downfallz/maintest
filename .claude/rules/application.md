---
paths:
  - "src/DownfallArena.Application/**"
  - "src/DownfallArena.Infrastructure/**"
  - "tools/**"
---

# Application, Infrastructure and tools rules

- Application declares ports as interfaces it owns (`IMatchRepository`, `IGameResources`). Infrastructure
  implements them. Domain never references a port.
- A field added to a route's answer gets one test through the route (`PackageTagsTests` reads the catalogue's),
  not only the projection's test and a spec whose mocked route writes the field by hand. A record the host
  sends (`SpellVisual`, `MutationVisual`, `CardMark`) gaining a field is such a change (#812, #1017).
- A use case is one class with one public method, named after the intent (`StartMatchHandler.Handle`).
  No generic "MatchService" bag of methods.
- Use cases load an aggregate, call one aggregate method, persist, and dispatch domain events. They do not
  contain game rules.
- Register services in `AddApplication` / `AddInfrastructure`. Do not use static service locators.
- Infrastructure has no game logic. If a rule ends up in an adapter, move it to the domain.
- Adding a framework (mediator, ORM, message bus) requires an ADR first.
- A list read from authored JSON (content, presentation, a scenario), of records or of ids, can hold `null`, and can
  be `null` itself, as can a single reference: `System.Text.Json` lets `[null]` and `"x": null` into a `List<T>` or a
  `string` it declares non-nullable (`AliasResolver.Resolve` refuses a null reference). The reader refuses it with an
  error that names the file and the place (`tutorial.steps[2]`, as `ScenarioFile` says it;
  `SpellArt.StagingProblems`), never with a `NullReferenceException`. Its test feeds it `[null]`
  (`SpellArtTests.A_null_among_the_beats_is_refused_and_not_a_crash`). The content builder under `tools/` reads
  `data/` through `GameSchemaBuilder`, which refuses both the same way
  (`GameSchemaBuilderTests.A_null_list_or_a_null_in_a_list_is_refused_with_its_file_and_place_and_not_a_crash`).
  Found three times in review (#883), and again in #890.
