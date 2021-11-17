using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Balance.Tests
{
    public class BattleResult
    {
        public List<Tuple<Spell, int>> Top5Spell { get; set; }
        public int WinningTeamNo { get; set; }
        public bool OneTeamDead { get; set; }
    
    }
}
