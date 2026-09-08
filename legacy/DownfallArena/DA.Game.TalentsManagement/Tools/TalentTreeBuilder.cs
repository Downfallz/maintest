using System.Collections.Generic;
using DA.Game.Domain.Models.TalentsManagement;
using DA.Game.Domain.Models.TalentsManagement.Enum;
using DA.Game.Domain.Models.TalentsManagement.Spells;

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
            List<Spell> berserkerTalents1 = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.Tornado),
                _getTalent.FromEnum(TalentList.PsychoRush),
            };
            List<Spell> soldierrTalents1 = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.ChainSlash),
                _getTalent.FromEnum(TalentList.ThunderingSeal)
            };
            List<Spell> paladinTalents1 = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.CrushingStomp),
                _getTalent.FromEnum(TalentList.RestorativeGush)
            };
            List<Spell> warlockTalents1 = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.RevenantGuards),
                _getTalent.FromEnum(TalentList.CrazedSpecters)
            };
            List<Spell> wizardTalents1 = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.IceSpear),
                _getTalent.FromEnum(TalentList.EngulfingFlames)
            };
            List<Spell> shamanTalents1 = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.ToxicWaves),
                _getTalent.FromEnum(TalentList.RestoringBurst)
            };
            List<Spell> assassinTalents1 = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.DeathSquad),
                _getTalent.FromEnum(TalentList.MortalWound)
            };
            List<Spell> beastMasterTalents1 = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.TranquilizerDart),
                _getTalent.FromEnum(TalentList.InfectiousBlast)
            };
            List<Spell> druidTalents1 = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.HatefulSacrifice),
                _getTalent.FromEnum(TalentList.SoulDevourer)
            };

            List<Spell> berserkerTalents2 = new List<Spell>() { _getTalent.FromEnum(TalentList.EnragedCharge) };
            List<Spell> soldierrTalents2 = new List<Spell>() { _getTalent.FromEnum(TalentList.ProtectiveSlam) };
            List<Spell> paladinTalents2 = new List<Spell>() { _getTalent.FromEnum(TalentList.FullPlate) };
            List<Spell> warlockTalents2 = new List<Spell>() { _getTalent.FromEnum(TalentList.SummonMinions) };
            List<Spell> wizardTalents2 = new List<Spell>() { _getTalent.FromEnum(TalentList.Meteor) };
            List<Spell> shamanTalents2 = new List<Spell>() { _getTalent.FromEnum(TalentList.HealingScreech) };
            List<Spell> assassinTalents2 = new List<Spell>() { _getTalent.FromEnum(TalentList.Momentum) };
            List<Spell> beastMasterTalents2 = new List<Spell>() { _getTalent.FromEnum(TalentList.NoxiousCure) };
            List<Spell> druidTalents2 = new List<Spell>() { _getTalent.FromEnum(TalentList.ParasiteJab) };


            List<List<Spell>> listListBerserker = new List<List<Spell>>
            {
                berserkerTalents1,
                berserkerTalents2
            };

            List<List<Spell>> listlistSoldier = new List<List<Spell>>
            {
                soldierrTalents1,
                soldierrTalents2
            };

            List<List<Spell>> listlistPaladin = new List<List<Spell>>
            {
                paladinTalents1,
                paladinTalents2
            };

            List<List<Spell>> listlistWarlock = new List<List<Spell>>
            {
                warlockTalents1,
                warlockTalents2
            };

            List<List<Spell>> listlistWizard = new List<List<Spell>>
            {
                wizardTalents1,
                wizardTalents2
            };

            List<List<Spell>> listlistShaman = new List<List<Spell>>
            {
                shamanTalents1,
                shamanTalents2
            };

            List<List<Spell>> listlistAssassin = new List<List<Spell>>
            {
                assassinTalents1,
                assassinTalents2
            };

            List<List<Spell>> listlistBeastMaster = new List<List<Spell>>
            {
                beastMasterTalents1,
                beastMasterTalents2
            };

            List<List<Spell>> listlistDruid = new List<List<Spell>>
            {
                druidTalents1,
                druidTalents2
            };

            TalentLevelLeaf sub1 = CreateSubCategory(listListBerserker);
            TalentLevelLeaf sub2 = CreateSubCategory(listlistSoldier);
            TalentLevelLeaf sub3 = CreateSubCategory(listlistPaladin);

            List<TalentLevelLeaf> listFighterSubcategories = new List<TalentLevelLeaf>
            {
                sub1,
                sub2,
                sub3
            };

            List<Spell> fighterTalents = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.Pummel),
                _getTalent.FromEnum(TalentList.Guard)
            };

            TalentLevelLeaf fighterMainLevel = CreateMainLevel(fighterTalents, listFighterSubcategories);

            TalentLevelLeaf sub4 = CreateSubCategory(listlistWarlock);
            TalentLevelLeaf sub5 = CreateSubCategory(listlistWizard);
            TalentLevelLeaf sub6 = CreateSubCategory(listlistShaman);

            List<TalentLevelLeaf> listMageSubcategories = new List<TalentLevelLeaf>
            {
                sub4,
                sub5,
                sub6
            };

            List<Spell> mageTalents = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.LightningBolt),
                _getTalent.FromEnum(TalentList.Rejuvenate)
            };

            TalentLevelLeaf mageMainLevel = CreateMainLevel(mageTalents, listMageSubcategories);

            TalentLevelLeaf sub7 = CreateSubCategory(listlistAssassin);
            TalentLevelLeaf sub8 = CreateSubCategory(listlistBeastMaster);
            TalentLevelLeaf sub9 = CreateSubCategory(listlistDruid);

            List<TalentLevelLeaf> listRangerSubcategories = new List<TalentLevelLeaf>
            {
                sub7,
                sub8,
                sub9
            };

            List<Spell> rangerTalents = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.PoisonSlash),
                _getTalent.FromEnum(TalentList.ThrowingStar)
            };

            TalentLevelLeaf rangerMainlevel = CreateMainLevel(rangerTalents, listRangerSubcategories);

            List<Spell> basicTalents = new List<Spell>()
            {
                _getTalent.FromEnum(TalentList.Strike),
                _getTalent.FromEnum(TalentList.HeavyStrike),
                _getTalent.FromEnum(TalentList.Wait),
            };

            List<TalentLevelLeaf> mainLevelsList = new List<TalentLevelLeaf>
            {
                fighterMainLevel,
                mageMainLevel,
                rangerMainlevel
            };

            TalentLevelLeaf basicLevel = CreateMainLevel(basicTalents, mainLevelsList);

            foreach (TalentNode beginningLevelTalentNode in basicLevel.TalentNodes)
            {
                beginningLevelTalentNode.IsUnlocked = true;
            }
            TalentTreeStructure newTalentTree = new TalentTreeStructure
            {
                Root = basicLevel
            };
            return newTalentTree;
        }



        private static TalentLevelLeaf CreateMainLevel(List<Spell> listTalentNameFirst1, List<TalentLevelLeaf> listLevels)
        {
            TalentLevelLeaf firstMainLevel = new TalentLevelLeaf();

            foreach (Spell s in listTalentNameFirst1)
            {
                firstMainLevel.TalentNodes.Add(new TalentNode { Spell = s });
            }

            if (listLevels != null)
            {
                foreach (TalentLevelLeaf lvl in listLevels)
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
            for (int i = 0; i < listListTalents.Count; i++)
            {
                level = CreateMainLevel(listListTalents[i], new List<TalentLevelLeaf>() { level });
            }

            return level;
        }
    }
}
