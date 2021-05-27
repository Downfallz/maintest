using System;

namespace DA.Core.Game.Main.Events
{
    public class CharacterTurnInitializedEventArgs : EventArgs
    {
        public Guid CharacterId { get; set; }
    }
}
