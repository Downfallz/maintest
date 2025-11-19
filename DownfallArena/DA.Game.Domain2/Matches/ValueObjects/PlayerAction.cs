namespace DA.Game.Domain2.Matches.ValueObjects;
public sealed record PlayerAction(string Kind, string? Payload = null);