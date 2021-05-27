using System;
using System.Collections.Generic;
using System.Linq;
using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Talents.Talents;
using DA.Core.Teams.Exceptions;

namespace DA.Core.Teams.TalentTree
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
        public IReadOnlyList<Spell> GetUnlockedTalents(TalentTreeStructure talentTreeStructure)
        {
            return GetAllUnlocked(talentTreeStructure.Root).Select(x => x.Spell).ToList().AsReadOnly();
        }

        public IReadOnlyList<Spell> GetUnlockableTalents(TalentTreeStructure talentTreeStructure)
        {
            return GetNextChildrenToUnlock(talentTreeStructure.Root).Select(x => x.Spell).ToList().AsReadOnly();
        }

        public IReadOnlyList<Spell> GetAllTalents(TalentTreeStructure talentTreeStructure)
        {
            return GetAll(talentTreeStructure.Root).Select(x => x.Spell).ToList().AsReadOnly();
        }

        public bool UnlockSpell(TalentTreeStructure talentTreeStructure, Spell talent)
        {
            var talentNode = GetNextChildrenToUnlock(talentTreeStructure.Root).SingleOrDefault(x => Object.ReferenceEquals(talent, x.Spell));
            if (talentNode == null)
                throw new TalentException("This Spell can not be unlocked yet or has already been unlocked.");
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
            foreach (var c in listLevels)
            {
                var atLeastOne = false;
                foreach (var s in c.TalentNodes)
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
            GetAll(listAll, new List<TalentLevelLeaf> {root});

            return listAll.Where(x => x.IsUnlocked).ToList().AsReadOnly();
        }

        private IReadOnlyList<TalentNode> GetAll(TalentLevelLeaf root)
        {
            IList<TalentNode> listAll = new List<TalentNode>();
            GetAll(listAll, new List<TalentLevelLeaf> {root});

            return listAll.ToList().AsReadOnly();
        }

        private void GetAll(IList<TalentNode> listAll, IEnumerable<TalentLevelLeaf> listLevels)
        {
            foreach (var c in listLevels)
            {
                foreach (var s in c.TalentNodes) listAll.Add(s);

                if (c.Children.Any()) GetAll(listAll, c.Children);
            }
        }
    }
}