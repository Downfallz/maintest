using System;
using System.Collections.Generic;
using System.Linq;

namespace DA.Game.Domain.Models.GameFlowEngine.TalentsManagement
{
    [Serializable]
    public class TalentLevelLeaf
    {
        public TalentLevelLeaf()
        {
            TalentNodes = new List<TalentNode>();
            Children = new List<TalentLevelLeaf>();
        }

        public TalentLevelLeaf Parent { get; set; }
        public List<TalentNode> TalentNodes { get; }
        public List<TalentLevelLeaf> Children { get; }


        public List<TalentNode> GetNextChildrenToUnlock()
        {
            List<TalentNode> listToUnlock = new List<TalentNode>();
            if (TalentNodes.Any(x => x.IsUnlocked))
            {
                GetAvailableToUnlock(listToUnlock, Children);
            }

            return listToUnlock.ToList();
        }

        private void GetAvailableToUnlock(List<TalentNode> listToUnlock, List<TalentLevelLeaf> listLevels)
        {
            foreach (TalentLevelLeaf c in listLevels)
            {
                bool atLeastOne = false;
                foreach (TalentNode s in c.TalentNodes)
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

        public List<TalentNode> GetAllUnlocked()
        {
            List<TalentNode> listAll = new List<TalentNode>();
            GetAll(listAll, new List<TalentLevelLeaf>() { this });

            return listAll.Where(x => x.IsUnlocked).ToList();
        }

        public List<TalentNode> GetAll()
        {
            List<TalentNode> listAll = new List<TalentNode>();
            GetAll(listAll, new List<TalentLevelLeaf>() { this });

            return listAll.ToList();
        }

        private void GetAll(List<TalentNode> listAll, List<TalentLevelLeaf> listLevels)
        {
            foreach (TalentLevelLeaf c in listLevels)
            {
                foreach (TalentNode s in c.TalentNodes)
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
