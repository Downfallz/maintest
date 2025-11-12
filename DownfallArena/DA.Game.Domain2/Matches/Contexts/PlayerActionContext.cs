using DA.Game.Domain2.Match.Enums;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.Ids;

namespace DA.Game.Domain2.Matches.Contexts;

public sealed record PlayerActionContext
{
    public PlayerSlot Slot { get; }
    public IReadOnlyList<CharacterStatus> MyCharacters { get; }
    public IReadOnlyList<CharacterStatus> EnemyCharacters { get; }

    private PlayerActionContext(PlayerSlot slot,
        IReadOnlyList<CharacterStatus> mine,
        IReadOnlyList<CharacterStatus> enemy)
    {
        Slot = slot;
        MyCharacters = mine;
        EnemyCharacters = enemy;
    }

    public static PlayerActionContext FromTeams(PlayerSlot slot, Team myTeam, Team enemyTeam)
        => new PlayerActionContext(
            slot,
            myTeam.Characters.Select(CharacterStatus.From).ToArray(),
            enemyTeam.Characters.Select(CharacterStatus.From).ToArray());

    // Aides métier
    public bool IsAvailable(CharacterId id) =>
        MyCharacters.Any(c => c.CharacterId == id && c.IsAlive && !c.IsStunned);

    public int AvailableCount => MyCharacters.Count(c => c.IsAlive && !c.IsStunned);
}
