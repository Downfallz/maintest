using System;
using System.Collections.Generic;
using System.Linq;
using DA.Core.Abilities.Talents.Abstractions;
using DA.Core.Abilities.Talents.Exceptions;
using DA.Core.Abilities.Talents.Models;

namespace DA.Core.Abilities.Talents
{
    public class TalentTreeService : ITalentTreeService
    {
        private readonly ITalentTreeStrucInitializer _talentTreeStrucInitializer;

        public TalentTreeService(ITalentTreeStrucInitializer talentTreeStrucInitializer)
        {
            _talentTreeStrucInitializer = talentTreeStrucInitializer;
        }

        public TalentTreeStructure InitializeNewTalentTree()
        {
            return _talentTreeStrucInitializer.GenerateNewTree();
        }
        public IReadOnlyList<Talent> GetUnlockedTalents(TalentTreeStructure talentTreeStructure)
        {
            return GetAllUnlocked(talentTreeStructure.Root).Select(x => x.Talent).ToList().AsReadOnly();
        }

        public IReadOnlyList<Talent> GetUnlockableTalents(TalentTreeStructure talentTreeStructure)
        {
            return GetNextChildrenToUnlock(talentTreeStructure.Root).Select(x => x.Talent).ToList().AsReadOnly();
        }

        public IReadOnlyList<Talent> GetAllTalents(TalentTreeStructure talentTreeStructure)
        {
            return GetAll(talentTreeStructure.Root).Select(x => x.Talent).ToList().AsReadOnly();
        }

        public bool UnlockTalent(TalentTreeStructure talentTreeStructure, Talent talent)
        {
            var talentNode = GetNextChildrenToUnlock(talentTreeStructure.Root).SingleOrDefault(x => Object.ReferenceEquals(talent, x.Talent));
            if (talentNode == null)
                throw new TalentException("This talent can not be unlocked yet.");
            talentNode.IsUnlocked = true;

            return true;
        }

        private IReadOnlyList<TalentNode> GetNextChildrenToUnlock(TalentLevelLeaf root)
        {
            IList<TalentNode> listToUnlock = new List<TalentNode>();
            if (root.TalentNodes.Any(x => x.IsUnlocked))
            {
                GetAvailableToUnlock(listToUnlock, root.Children);
            }

            return listToUnlock.ToList().AsReadOnly();
        }

        private void GetAvailableToUnlock(IList<TalentNode> listToUnlock, IEnumerable<TalentLevelLeaf> listLevels)
        {
            foreach (var c in listLevels)
            {
                bool atLeastOne = false;
                foreach (var s in c.TalentNodes)
                {
                    if (!s.IsUnlocked)
                        listToUnlock.Add(s);
                    else
                    {
                        atLeastOne = true;
                    }
                }

                if (c.Children.Any() && atLeastOne)
                {
                    GetAvailableToUnlock(listToUnlock, c.Children);
                }
            }
        }

        private IReadOnlyList<TalentNode> GetAllUnlocked(TalentLevelLeaf root)
        {
            IList<TalentNode> listAll = new List<TalentNode>();
            GetAll(listAll, new List<TalentLevelLeaf>() { root });

            return listAll.Where(x => x.IsUnlocked).ToList().AsReadOnly();
        }

        private IReadOnlyList<TalentNode> GetAll(TalentLevelLeaf root)
        {
            IList<TalentNode> listAll = new List<TalentNode>();
            GetAll(listAll, new List<TalentLevelLeaf>() { root });

            return listAll.ToList().AsReadOnly();
        }

        private void GetAll(IList<TalentNode> listAll, IEnumerable<TalentLevelLeaf> listLevels)
        {
            foreach (var c in listLevels)
            {
                foreach (var s in c.TalentNodes)
                {
                    listAll.Add(s);
                }

                if (c.Children.Any())
                {
                    GetAll(listAll, c.Children);
                }
            }
        }
    }
}
