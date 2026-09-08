# 0008. Use cases and domain events without a mediator library

Date: 2026-09-08
Status: Accepted

## Context

The legacy application used MediatR for commands, queries, notifications, and pipeline behaviours, and the
domain referenced MediatR so that domain events could be notifications. MediatR is now commercially licensed,
the pipelines added little (a logging behaviour, a validation behaviour, a unit of work that never fired for
generic results), and the coupling leaked a framework into the domain.

## Decision

We will define, in Application, `ICommandHandler<TCommand, TResult>` and `IQueryHandler<TQuery, TResult>`,
one implementation per use case, registered in dependency injection and resolved directly by hosts. Domain
events stay plain records implementing the framework-free `IDomainEvent`; after a repository save, the
application dispatches the aggregate's events to `IDomainEventListener<TEvent>` implementations, then clears
them. No dispatcher library, no reflection-based pipeline.

## Consequences

- Good: no licence, no framework in Domain, handlers are ordinary classes that tests call directly.
- Bad: cross-cutting behaviour (logging, validation) is added by decorators when needed, by hand.
- Neutral: if pipelines become real, a library (Wolverine, Mediator source generator) gets its own ADR.

## Alternatives considered

- Keep MediatR: licence cost and the domain coupling it encouraged.
- Wolverine or a source-generated mediator: capable, but nothing in the engine needs them yet.

## Follow-up

- Phase 8 introduces the handler interfaces and the event dispatch. The event handler base is named
  `DomainEventListener<TEvent>`: analyzer CA1711 reserves the `EventHandler` suffix for delegates.
