using DA.Game.Shared.Contracts.Matches.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Matches.ReadModels;

public sealed record PlayerUnlockableSpellsView(
    PlayerSlot Slot,
    IReadOnlyList<CreatureUnlockableSpellsView> Creatures);