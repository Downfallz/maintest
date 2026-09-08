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
