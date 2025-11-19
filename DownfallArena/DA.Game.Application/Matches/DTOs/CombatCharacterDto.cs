using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Matches.DTOs;

public sealed record CombatCharacterDto(
    CharacterId Id,
    IReadOnlyList<SpellId> StartingSpellIds,
    int BaseHealth,
    int BaseEnergy,
    int BaseDefense,
    int BaseInitiative,
    double BaseCritical,
    int Health,
    int Energy,
    int ExtraPoint,
    int BonusDefense,
    double BonusCritical,
    int CurrentInitiative,
    int BonusRetaliate,
    bool IsStunned,
    bool IsAlive,
    bool IsDead
);