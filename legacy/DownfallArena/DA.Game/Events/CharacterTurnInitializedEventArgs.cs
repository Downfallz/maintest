using System;
using DA.Game.Domain.Models.CombatMechanic;

namespace DA.Game.Events
{
    public class CharacterPlayedEventArgs : EventArgs
    {
        public SpellResolverResult SpellResolverResult { get; set; }
    }
}
