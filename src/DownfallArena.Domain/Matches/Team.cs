using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches;

/// <summary>
/// The creatures a player commands. Defeated when none of them is alive.
/// </summary>
public sealed class Team
{
    private readonly List<Creature> _creatures;

    private Team(PlayerSlot owner, List<Creature> creatures)
    {
        Owner = owner;
        _creatures = creatures;
    }

    public PlayerSlot Owner { get; }

    public IReadOnlyList<Creature> Creatures => _creatures;

    public IEnumerable<Creature> LivingCreatures => _creatures.Where(creature => creature.IsAlive);

    public bool IsDefeated => _creatures.TrueForAll(creature => creature.IsDead);

    public int TotalHealth => _creatures.Sum(creature => creature.Health.Value);

    public static Team Form(PlayerSlot owner, IReadOnlyList<Creature> creatures)
    {
        ArgumentNullException.ThrowIfNull(creatures);

        if (creatures.Count == 0)
        {
            throw new ArgumentException("A team needs at least one creature.", nameof(creatures));
        }

        if (creatures.Any(creature => creature.Owner != owner))
        {
            throw new ArgumentException("Every creature of a team must belong to the team's player slot.", nameof(creatures));
        }

        if (creatures.Select(creature => creature.Id).Distinct().Count() != creatures.Count)
        {
            throw new ArgumentException("Creature ids within a team must be unique.", nameof(creatures));
        }

        return new Team(owner, [.. creatures]);
    }

    public Creature? Find(CreatureId id) => _creatures.Find(creature => creature.Id == id);
}
