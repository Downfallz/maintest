namespace DownfallArena.Application.Learning;

/// <summary>
/// The numeric view of a match for one player at one decision: a fixed-length feature vector and the schema
/// version that says what each index means.
/// </summary>
public sealed record Observation(string SchemaVersion, IReadOnlyList<float> Features)
{
    public bool Equals(Observation? other) =>
        other is not null && SchemaVersion == other.SchemaVersion && Features.SequenceEqual(other.Features);

    public override int GetHashCode() => HashCode.Combine(SchemaVersion, Features.Count);
}
