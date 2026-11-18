---
paths:
  - "src/DownfallArena.Application/**"
  - "src/DownfallArena.Infrastructure/**"
---

# Application and Infrastructure rules

- Application declares ports as interfaces it owns (`IMatchRepository`, `IGameResources`). Infrastructure
  implements them. Domain never references a port.
- A use case is one class with one public method, named after the intent (`StartMatchHandler.Handle`).
  No generic "MatchService" bag of methods.
- Use cases load an aggregate, call one aggregate method, persist, and dispatch domain events. They do not
  contain game rules.
- Register services in `AddApplication` / `AddInfrastructure`. Do not use static service locators.
- Infrastructure has no game logic. If a rule ends up in an adapter, move it to the domain.
- Adding a framework (mediator, ORM, message bus) requires an ADR first.
- A list of records read from authored JSON (content, presentation, a scenario) can hold `null`: `System.Text.Json`
  lets `[null]` into a `List<T>` of records. The reader refuses it with an error that names the file and the place
  (`tutorial.steps[2]`, as `ScenarioFile` says it; `SpellArt.StagingProblems`), never with a `NullReferenceException`.
  Its test feeds it `[null]` (`SpellArtTests.A_null_among_the_beats_is_refused_and_not_a_crash`). Found three times in
  review (#883).
