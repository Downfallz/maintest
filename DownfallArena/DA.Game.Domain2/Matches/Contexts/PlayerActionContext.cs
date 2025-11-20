using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;

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
    {
        ArgumentNullException.ThrowIfNull(myTeam);
        ArgumentNullException.ThrowIfNull(enemyTeam);

        return new PlayerActionContext(
            slot,
            [..myTeam.Characters.Select(CharacterStatus.From)],
            [..enemyTeam.Characters.Select(CharacterStatus.From)]);
    }

    // Aides métier
    public bool IsAvailable(CharacterId id) =>
        MyCharacters.Any(c => c.CharacterId == id && c.IsAlive && !c.IsStunned);

    public int MyAvailableCharactersCount => MyCharacters.Count(c => c.IsAlive && !c.IsStunned);

    public int EnemyAvailableCharactersCount => EnemyCharacters.Count(c => c.IsAlive && !c.IsStunned);

    public int AllAvailableCharactersCount => MyAvailableCharactersCount + EnemyAvailableCharactersCount;
}
