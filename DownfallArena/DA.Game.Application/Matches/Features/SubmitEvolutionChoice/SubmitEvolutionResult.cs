using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Utilities;

namespace DA.Game.Application.Matches.Features.SubmitEvolutionChoice;

public sealed record SubmitEvolutionResult(HashSet<SpellUnlockChoice> CurrentChoicesForPlayer, RoundPhase State) : ValueObject;
