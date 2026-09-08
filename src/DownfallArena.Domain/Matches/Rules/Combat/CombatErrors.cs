using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Rules.Combat;

public static class CombatErrors
{
    public static readonly DomainError UnknownCreature = new("Combat.UnknownCreature", "No creature with this id takes part in the match.");

    public static readonly DomainError NotYourCreature = new("Combat.NotYourCreature", "A player can only act with their own creatures.");

    public static readonly DomainError ActorDead = new("Combat.ActorDead", "A dead creature cannot act.");

    public static readonly DomainError ActorStunned = new("Combat.ActorStunned", "A stunned creature cannot act.");

    public static readonly DomainError SpellNotKnown = new("Combat.SpellNotKnown", "The creature does not know this spell.");

    public static readonly DomainError NotEnoughEnergy = new("Combat.NotEnoughEnergy", "The creature cannot afford this spell.");

    public static readonly DomainError NoTargets = new("Combat.NoTargets", "This spell needs at least one target.");

    public static readonly DomainError ExactlyOneTarget = new("Combat.ExactlyOneTarget", "This spell targets exactly one creature.");

    public static readonly DomainError TooManyTargets = new("Combat.TooManyTargets", "Too many targets for this spell.");

    public static readonly DomainError DuplicateTargets = new("Combat.DuplicateTargets", "A creature cannot be targeted twice by the same action.");

    public static readonly DomainError SelfOnly = new("Combat.SelfOnly", "This spell can only target its caster.");

    public static readonly DomainError AlliesOnly = new("Combat.AlliesOnly", "This spell can only target allies.");

    public static readonly DomainError EnemiesOnly = new("Combat.EnemiesOnly", "This spell can only target enemies.");

    public static readonly DomainError UnknownTarget = new("Combat.UnknownTarget", "No creature with this id takes part in the match.");

    public static readonly DomainError TargetDead = new("Combat.TargetDead", "A dead creature cannot be targeted.");

    public static readonly DomainError NoLegalTarget = new("Combat.NoLegalTarget", "This spell has no legal target right now; the intent is revealed without targets and fizzles.");

    public static readonly DomainError AllTargetsInvalid = new("Combat.AllTargetsInvalid", "None of the chosen targets is valid any more.");
}
