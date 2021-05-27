using System.Collections.Generic;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;

namespace DA.Game.TalentsManagement.Tools
{
    public class TalentTreeBuilder : ITalentTreeBuilder
    {
        private readonly IGetSpell _getTalent;

        public TalentTreeBuilder(IGetSpell getTalent)
        {
            this._getTalent = getTalent;
        }

        public TalentTreeStructure GenerateNewTree()
        {
            var berserkerTalents1 = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.Tornado),
                _getTalent.FromEnum(TalentList.PsychoRush),
            };
            var soldierrTalents1 = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.ChainSlash),
                _getTalent.FromEnum(TalentList.ThunderingSeal)
            };
            var paladinTalents1 = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.CrushingStomp),
                _getTalent.FromEnum(TalentList.RestorativeGush)
            };
            var warlockTalents1 = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.RevenantGuards),
                _getTalent.FromEnum(TalentList.CrazedSpecters)
            };
            var wizardTalents1 = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.IceSpear),
                _getTalent.FromEnum(TalentList.EngulfingFlames)
            };
            var shamanTalents1 = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.ToxicWaves),
                _getTalent.FromEnum(TalentList.RestoringBurst)
            };
            var assassinTalents1 = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.DeathSquad),
                _getTalent.FromEnum(TalentList.MortalWound)
            };
            var beastMasterTalents1 = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.TranquilizerDart),
                _getTalent.FromEnum(TalentList.InfectiousBlast)
            };
            var druidTalents1 = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.HatefulSacrifice),
                _getTalent.FromEnum(TalentList.SoulDevourer)
            };

            var berserkerTalents2 = new List<Spell>() { _getTalent.FromEnum(TalentList.EnragedCharge) };
            var soldierrTalents2 = new List<Spell>() { _getTalent.FromEnum(TalentList.ProtectiveSlam) };
            var paladinTalents2 = new List<Spell>() { _getTalent.FromEnum(TalentList.FullPlate) };
            var warlockTalents2 = new List<Spell>() { _getTalent.FromEnum(TalentList.SummonMinions) };
            var wizardTalents2 = new List<Spell>() { _getTalent.FromEnum(TalentList.Meteor) };
            var shamanTalents2 = new List<Spell>() { _getTalent.FromEnum(TalentList.HealingScreech) };
            var assassinTalents2 = new List<Spell>() { _getTalent.FromEnum(TalentList.Momentum) };
            var beastMasterTalents2 = new List<Spell>() { _getTalent.FromEnum(TalentList.NoxiousCure) };
            var druidTalents2 = new List<Spell>() { _getTalent.FromEnum(TalentList.ParasiteJab) };


            var listListBerserker = new List<List<Spell>>();
            listListBerserker.Add(berserkerTalents1);
            listListBerserker.Add(berserkerTalents2);

            var listlistSoldier = new List<List<Spell>>();
            listlistSoldier.Add(soldierrTalents1);
            listlistSoldier.Add(soldierrTalents2);

            var listlistPaladin = new List<List<Spell>>();
            listlistPaladin.Add(paladinTalents1);
            listlistPaladin.Add(paladinTalents2);

            var listlistWarlock = new List<List<Spell>>();
            listlistWarlock.Add(warlockTalents1);
            listlistWarlock.Add(warlockTalents2);

            var listlistWizard = new List<List<Spell>>();
            listlistWizard.Add(wizardTalents1);
            listlistWizard.Add(wizardTalents2);

            var listlistShaman = new List<List<Spell>>();
            listlistShaman.Add(shamanTalents1);
            listlistShaman.Add(shamanTalents2);

            var listlistAssassin = new List<List<Spell>>();
            listlistAssassin.Add(assassinTalents1);
            listlistAssassin.Add(assassinTalents2);

            var listlistBeastMaster = new List<List<Spell>>();
            listlistBeastMaster.Add(beastMasterTalents1);
            listlistBeastMaster.Add(beastMasterTalents2);

            var listlistDruid = new List<List<Spell>>();
            listlistDruid.Add(druidTalents1);
            listlistDruid.Add(druidTalents2);

            var sub1 = CreateSubCategory(listListBerserker);
            var sub2 = CreateSubCategory(listlistSoldier);
            var sub3 = CreateSubCategory(listlistPaladin);

            var listFighterSubcategories = new List<TalentLevelLeaf>();
            listFighterSubcategories.Add(sub1);
            listFighterSubcategories.Add(sub2);
            listFighterSubcategories.Add(sub3);

            var fighterTalents = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.Pummel),
                _getTalent.FromEnum(TalentList.Guard)
            };

            var fighterMainLevel = CreateMainLevel(fighterTalents, listFighterSubcategories);

            var sub4 = CreateSubCategory(listlistWarlock);
            var sub5 = CreateSubCategory(listlistWizard);
            var sub6 = CreateSubCategory(listlistShaman);

            var listMageSubcategories = new List<TalentLevelLeaf>();
            listMageSubcategories.Add(sub4);
            listMageSubcategories.Add(sub5);
            listMageSubcategories.Add(sub6);

            var mageTalents = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.LightningBolt),
                _getTalent.FromEnum(TalentList.Rejuvenate)
            };

            var mageMainLevel = CreateMainLevel(mageTalents, listMageSubcategories);

            var sub7 = CreateSubCategory(listlistAssassin);
            var sub8 = CreateSubCategory(listlistBeastMaster);
            var sub9 = CreateSubCategory(listlistDruid);

            var listRangerSubcategories = new List<TalentLevelLeaf>();
            listRangerSubcategories.Add(sub7);
            listRangerSubcategories.Add(sub8);
            listRangerSubcategories.Add(sub9);

            var rangerTalents = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.PoisonSlash),
                _getTalent.FromEnum(TalentList.ThrowingStar)
            };

            var rangerMainlevel = CreateMainLevel(rangerTalents, listRangerSubcategories);

            var basicTalents = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.Strike),
                _getTalent.FromEnum(TalentList.HeavyStrike),
                _getTalent.FromEnum(TalentList.Wait),
            };

            var mainLevelsList = new List<TalentLevelLeaf>();
            mainLevelsList.Add(fighterMainLevel);
            mainLevelsList.Add(mageMainLevel);
            mainLevelsList.Add(rangerMainlevel);

            var basicLevel = CreateMainLevel(basicTalents, mainLevelsList);

            foreach (var beginningLevelTalentNode in basicLevel.TalentNodes)
            {
                beginningLevelTalentNode.IsUnlocked = true;
            }
            var newTalentTree = new TalentTreeStructure();
            newTalentTree.Root = basicLevel;
            return newTalentTree;
        }



        private static TalentLevelLeaf CreateMainLevel(List<Spell> listTalentNameFirst1, List<TalentLevelLeaf> listLevels)
        {
            var firstMainLevel = new TalentLevelLeaf();

            foreach (var s in listTalentNameFirst1)
            {
                firstMainLevel.TalentNodes.Add(new TalentNode{Spell = s});
            }

            if (listLevels != null)
            {
                foreach (var lvl in listLevels)
                {
                    if (lvl != null)
                    {
                        firstMainLevel.Children.Add(lvl);
                        lvl.Parent = firstMainLevel;
                    }
                }
            }

            return firstMainLevel;
        }

        private static TalentLevelLeaf CreateSubCategory(List<List<Spell>> listListTalents)
        {
            TalentLevelLeaf level = null;
            for (var i = 0; i < listListTalents.Count; i++)
            {
                level = CreateMainLevel(listListTalents[i], new List<TalentLevelLeaf>() { level });
            }

            return level;
        }
    }
}
