using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;

namespace DownfallArena.Application.Learning;

/// <summary>
/// Builds the observation of a player's board under a feature schema. Pure and deterministic: the same board
/// gives the same vector, and the two players' vectors mirror each other.
/// </summary>
public sealed class ObservationBuilder(FeatureSchema schema, IGameResources resources)
{
    /// <summary>The "remaining rounds" value of a permanent condition.</summary>
    public const float PermanentCondition = -1f;

    public FeatureSchema Schema => schema;

    public Observation Build(PlayerBoardState board)
    {
        ArgumentNullException.ThrowIfNull(board);

        var slots = BoardSlots.Of(board, schema.TeamSize);
        var features = new float[schema.Length];
        WriteGlobals(board, slots, features);
        for (var index = 0; index < board.Allies.Count; index++)
        {
            WriteCreature(board.Allies[index], features, schema.CreatureOffset(index));
        }

        for (var index = 0; index < board.Enemies.Count; index++)
        {
            WriteCreature(board.Enemies[index], features, schema.CreatureOffset(schema.TeamSize + index));
        }

        return new Observation(schema.Id, features);
    }

    private void WriteGlobals(PlayerBoardState board, BoardSlots slots, float[] features)
    {
        features[0] = board.RoundNumber is { } round ? (float)round / schema.RoundCap : 0f;
        features[1] = board.Phase is { } phase ? (float)phase / 3f : 0f;
        features[2] = board.SubPhase is { } subPhase ? (float)subPhase / 9f : 0f;
        features[3] = board.Timeline.Count == 0 ? 0f : (float)board.RevealCursor / board.Timeline.Count;
        features[4] = (float)board.RevealedActions.Count(action => !slots.IsOwn(slots.SlotOf(action.Actor))) / schema.TeamSize;
    }

    private void WriteCreature(CreatureSnapshot creature, float[] features, int offset)
    {
        features[offset] = creature.IsAlive ? 1f : 0f;
        features[offset + 1] = creature.MaxHealth.Value == 0 ? 0f : (float)creature.Health.Value / creature.MaxHealth.Value;
        features[offset + 2] = creature.Energy.Value;
        features[offset + 3] = creature.IsStunned ? 1f : 0f;
        features[offset + 4] = creature.TotalDefense.Value;
        features[offset + 5] = creature.CurrentInitiative.Value;

        var conditions = offset + FeatureSchema.CreatureFeatures.Count;
        WriteConditions(creature, features, conditions);
        var spells = conditions + (2 * FeatureSchema.ConditionKinds.Count);
        WriteSpells(creature, features, spells);
        WriteNodes(creature, features, spells + schema.Spells.Count);
    }

    private static void WriteConditions(CreatureSnapshot creature, float[] features, int offset)
    {
        foreach (var condition in creature.Conditions)
        {
            var amountIndex = offset + (2 * KindIndex(condition.Effect));
            features[amountIndex] += Amount(condition.Effect);
            var remaining = condition.RemainingRounds is { } rounds ? rounds : PermanentCondition;
            features[amountIndex + 1] = Combine(features[amountIndex + 1], remaining);
        }
    }

    private void WriteSpells(CreatureSnapshot creature, float[] features, int offset)
    {
        foreach (var index in creature.KnownSpells.Select(schema.SpellIndex).Where(index => index >= 0))
        {
            features[offset + index] = 1f;
        }
    }

    /// <summary>A node counts as unlocked once every spell it offers is known.</summary>
    private void WriteNodes(CreatureSnapshot creature, float[] features, int offset)
    {
        var tree = resources.GetTalentTree(creature.TalentTree);
        var unlocked = tree.Nodes
            .Where(node => node.Spells.Count > 0 && node.Spells.All(spell => creature.KnowsSpell(spell.Id)))
            .Select(node => schema.TalentNodeIndex(FeatureSchema.NodeKey(tree.Id, node.Code)))
            .Where(index => index >= 0);
        foreach (var index in unlocked)
        {
            features[offset + index] = 1f;
        }
    }

    /// <summary>The longest remaining duration wins; a permanent condition beats any number of rounds.</summary>
    private static float Combine(float current, float remaining) =>
        current < 0f || remaining < 0f ? PermanentCondition : Math.Max(current, remaining);

    private static int KindIndex(LastingEffect effect)
    {
        var index = FeatureSchema.ConditionKindIndex(effect.GetType().Name);
        return index >= 0 ? index : throw Unpublished(effect);
    }

    private static float Amount(LastingEffect effect) =>
        effect switch
        {
            Bleed bleed => bleed.AmountPerRound,
            Regeneration regeneration => regeneration.AmountPerRound,
            EnergyRegeneration energyRegeneration => energyRegeneration.AmountPerRound,
            Stun => 1f,
            DefenseBuff buff => buff.Amount,
            InitiativeDebuff debuff => debuff.Amount,
            _ => throw Unpublished(effect),
        };

    private static InvalidOperationException Unpublished(LastingEffect effect) =>
        new($"Condition kind '{effect.GetType().Name}' is not in feature schema {FeatureSchema.CurrentVersion}; publish a new version.");
}
