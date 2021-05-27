using System;
using System.Collections.Generic;
using System.Linq;

namespace DA.Game.Domain.Models.GameFlowEngine
{
    [Serializable]
    public class Team
    {
        public Team()
        {
            Characters = new List<Character>();
        }
        
        public IList<Character> Characters { get; set; }

        public IList<Character> AliveCharacters => Characters.Where(x => !x.IsDead).ToList();

        public bool IsDead => Characters.All(x => x.IsDead);
    }
}
