namespace DA.Game.Shared.Contracts.Matches.Ids;

public readonly record struct CharacterId(int Value) {
    public static CharacterId New(int value) => new(value);
    public override string ToString() => Value.ToString();
}
