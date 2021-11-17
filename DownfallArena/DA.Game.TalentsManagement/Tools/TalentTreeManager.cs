using System;
using System.Collections.Generic;
using System.Linq;
using DA.Game.Domain.Models.TalentsManagement;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.TalentsManagement.Tools
{
    public class TalentTreeManager : ITalentTreeManager
    {
        private readonly ITalentTreeBuilder _talentTreeStrucInitializer;

        public TalentTreeManager(ITalentTreeBuilder talentTreeStrucInitializer)
        {
            _talentTreeStrucInitializer = talentTreeStrucInitializer;
        }

        public TalentTreeStructure InitializeNewTalentTree()
        {
            return _talentTreeStrucInitializer.GenerateNewTree();
        }
        public IReadOnlyList<Spell> GetUnlockedSpells(TalentTreeStructure talentTreeStructure)
        {
            return GetAllUnlocked(talentTreeStructure.Root).Select(x => x.Spell).ToList().AsReadOnly();
        }

        public IReadOnlyList<Spell> GetUnlockableSpells(TalentTreeStructure talentTreeStructure)
        {
            return GetNextChildrenToUnlock(talentTreeStructure.Root).Select(x => x.Spell).ToList().AsReadOnly();
        }

        public IReadOnlyList<Spell> GetAllSpells(TalentTreeStructure talentTreeStructure)
        {
            return GetAll(talentTreeStructure.Root).Select(x => x.Spell).ToList().AsReadOnly();
        }

        public bool UnlockSpell(TalentTreeStructure talentTreeStructure, Spell talent)
        {
            TalentNode talentNode = talentTreeStructure.Root.GetNextChildrenToUnlock().SingleOrDefault(x => x.Spell.Name == talent.Name);
            if (talentNode == null)
            {
                string weirtd = ""; //;throw new Exception("This Spell can not be unlocked yet or has already been unlocked.");
            }

            talentNode.IsUnlocked = true;

            return true;
        }

        private IReadOnlyList<TalentNode> GetNextChildrenToUnlock(TalentLevelLeaf root)
        {
            IList<TalentNode> listToUnlock = new List<TalentNode>();
            if (root.TalentNodes.Any(x => x.IsUnlocked)) GetAvailableToUnlock(listToUnlock, root.Children);

            return listToUnlock.ToList().AsReadOnly();
        }

        private void GetAvailableToUnlock(IList<TalentNode> listToUnlock, IEnumerable<TalentLevelLeaf> listLevels)
        {
            foreach (TalentLevelLeaf c in listLevels)
            {
                bool atLeastOne = false;
                foreach (TalentNode s in c.TalentNodes)
                    if (!s.IsUnlocked)
                        listToUnlock.Add(s);
                    else
                        atLeastOne = true;

                if (c.Children.Any() && atLeastOne) GetAvailableToUnlock(listToUnlock, c.Children);
            }
        }

        private IReadOnlyList<TalentNode> GetAllUnlocked(TalentLevelLeaf root)
        {
            IList<TalentNode> listAll = new List<TalentNode>();
            GetAll(listAll, new List<TalentLevelLeaf> { root });

            return listAll.Where(x => x.IsUnlocked).ToList().AsReadOnly();
        }

        private IReadOnlyList<TalentNode> GetAll(TalentLevelLeaf root)
        {
            IList<TalentNode> listAll = new List<TalentNode>();
            GetAll(listAll, new List<TalentLevelLeaf> { root });

            return listAll.ToList().AsReadOnly();
        }

        private void GetAll(IList<TalentNode> listAll, IEnumerable<TalentLevelLeaf> listLevels)
        {
            foreach (TalentLevelLeaf c in listLevels)
            {
                foreach (TalentNode s in c.TalentNodes) listAll.Add(s);

                if (c.Children.Any()) GetAll(listAll, c.Children);
            }
        }
    }
}