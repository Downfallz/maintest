namespace DA.Game.Domain2.Match.ValueObjects;
public sealed record PlayerAction(string Kind, string? Payload = null);