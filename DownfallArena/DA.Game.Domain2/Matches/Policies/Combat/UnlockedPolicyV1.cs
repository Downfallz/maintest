using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Policies.Combat;

public sealed class UnlockedPolicyV1 : IUnlockedPolicy
{
    private const string DOM_DX01_LOCKED_SPELL =
        "DX01 - Actor cannot perform this combat action as this spell is locked.";

    public Result EnsureCreatureHasUnlockedSpell(CreaturePerspective ctx, Spell spell)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(spell);

        var spellId = spell.Id;
        var knownSpellIds = ctx.Actor.KnownSpellIds;

        if (!knownSpellIds.Contains(spellId))
            return Result.Fail(DOM_DX01_LOCKED_SPELL);

        return Result.Ok();
    }
}
