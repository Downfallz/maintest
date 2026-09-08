using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Policies.Combat;

public interface IUnlockedPolicy
{
    Result EnsureCreatureHasUnlockedSpell(CreaturePerspective ctx, Spell spell);
}
