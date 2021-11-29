using System;

namespace DA.Game.Events
{
    public class CharacterTurnInitializedEventArgs : EventArgs
    {
        public Guid CharacterId { get; set; }
    }
}
