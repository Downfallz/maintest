namespace DownfallArena.Application.Learning;

/// <summary>
/// The numeric view of a match for one player at one decision: a fixed-length feature vector and the id of the
/// schema (<see cref="FeatureSchema.Id"/>) that says what each index means.
/// </summary>
public sealed record Observation(string SchemaId, IReadOnlyList<float> Features)
{
    public bool Equals(Observation? other) =>
        other is not null && SchemaId == other.SchemaId && Features.SequenceEqual(other.Features);

    public override int GetHashCode() => HashCode.Combine(SchemaId, Features.Count);
}
