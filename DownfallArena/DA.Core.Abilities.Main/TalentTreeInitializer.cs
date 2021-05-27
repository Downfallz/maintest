using System.Collections.Generic;
using DA.Core.Abilities.Talents;
using DA.Core.Abilities.Talents.Abstractions;
using DA.Core.Abilities.Talents.Models;

namespace DA.Core.Abilities.Main
{
    public class TalentTreeInitializer : ITalentTreeStrucInitializer
    {
        private readonly IGetTalent _getTalent;

        public TalentTreeInitializer(IGetTalent getTalent)
        {
            this._getTalent = getTalent;
        }

        public TalentTreeStructure GenerateNewTree()
        {
            var berserkerTalents0 = new List<Talent>() { _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Berserker4) };
            var soldierrTalents0 = new List<Talent>() { _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Soldier4) };
            var paladinTalents0 = new List<Talent>() { _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Paladin4) };
            var warlockTalents0 = new List<Talent>() { _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Warlock4) };
            var wizardTalents0 = new List<Talent>() { _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Wizard4) };
            var shamanTalents0 = new List<Talent>() { _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Shaman4) };
            var assassinTalents0 = new List<Talent>() { _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Assassin4) };
            var beastMasterTalents0 = new List<Talent>() { _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.BeastMaster4) };
            var druidTalents0 = new List<Talent>() { _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Druid4) };

            var berserkerTalents1 = new List<Talent>()
            {
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Berserker2),
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Berserker3),
            };
            var soldierrTalents1 = new List<Talent>()
            {
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Soldier2),
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Soldier3)
            };
            var paladinTalents1 = new List<Talent>()
            {
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Paladin2),
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Paladin3)
            };
            var warlockTalents1 = new List<Talent>()
            {
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Warlock2),
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Warlock3)
            };
            var wizardTalents1 = new List<Talent>()
            {
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Wizard2),
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Wizard3)
            };
            var shamanTalents1 = new List<Talent>()
            {
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Shaman2),
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Shaman3)
            };
            var assassinTalents1 = new List<Talent>()
            {
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Assassin2),
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Assassin3)
            };
            var beastMasterTalents1 = new List<Talent>()
            {
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.BeastMaster2),
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.BeastMaster3)
            };
            var druidTalents1 = new List<Talent>()
            {
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Druid2),
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Druid3)
            };

            var berserkerTalents2 = new List<Talent>() { _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Berserker1) };
            var soldierrTalents2 = new List<Talent>() { _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Soldier1) };
            var paladinTalents2 = new List<Talent>() { _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Paladin1) };
            var warlockTalents2 = new List<Talent>() { _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Warlock1) };
            var wizardTalents2 = new List<Talent>() { _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Wizard1) };
            var shamanTalents2 = new List<Talent>() { _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Shaman1) };
            var assassinTalents2 = new List<Talent>() { _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Assassin1) };
            var beastMasterTalents2 = new List<Talent>() { _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.BeastMaster1) };
            var druidTalents2 = new List<Talent>() { _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Druid1) };


            var listListBerserker = new List<List<Talent>>();
            listListBerserker.Add(berserkerTalents0);
            listListBerserker.Add(berserkerTalents1);
            listListBerserker.Add(berserkerTalents2);

            var listlistSoldier = new List<List<Talent>>();
            listlistSoldier.Add(soldierrTalents0);
            listlistSoldier.Add(soldierrTalents1);
            listlistSoldier.Add(soldierrTalents2);

            var listlistPaladin = new List<List<Talent>>();
            listlistPaladin.Add(paladinTalents0);
            listlistPaladin.Add(paladinTalents1);
            listlistPaladin.Add(paladinTalents2);

            var listlistWarlock = new List<List<Talent>>();
            listlistWarlock.Add(warlockTalents0);
            listlistWarlock.Add(warlockTalents1);
            listlistWarlock.Add(warlockTalents2);

            var listlistWizard = new List<List<Talent>>();
            listlistWizard.Add(wizardTalents0);
            listlistWizard.Add(wizardTalents1);
            listlistWizard.Add(wizardTalents2);

            var listlistShaman = new List<List<Talent>>();
            listlistShaman.Add(shamanTalents0);
            listlistShaman.Add(shamanTalents1);
            listlistShaman.Add(shamanTalents2);

            var listlistAssassin = new List<List<Talent>>();
            listlistAssassin.Add(assassinTalents0);
            listlistAssassin.Add(assassinTalents1);
            listlistAssassin.Add(assassinTalents2);

            var listlistBeastMaster = new List<List<Talent>>();
            listlistBeastMaster.Add(beastMasterTalents0);
            listlistBeastMaster.Add(beastMasterTalents1);
            listlistBeastMaster.Add(beastMasterTalents2);

            var listlistDruid = new List<List<Talent>>();
            listlistDruid.Add(druidTalents0);
            listlistDruid.Add(druidTalents1);
            listlistDruid.Add(druidTalents2);

            var sub1 = CreateSubCategory(listListBerserker);
            var sub2 = CreateSubCategory(listlistSoldier);
            var sub3 = CreateSubCategory(listlistPaladin);

            var listFighterSubcategories = new List<TalentLevelLeaf>();
            listFighterSubcategories.Add(sub1);
            listFighterSubcategories.Add(sub2);
            listFighterSubcategories.Add(sub3);

            var fighterTalents = new List<Talent>()
            {
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.FighterAttack1),
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.FighterAttack2)
            };

            var fighterMainLevel = CreateMainLevel(fighterTalents, listFighterSubcategories);

            var sub4 = CreateSubCategory(listlistWarlock);
            var sub5 = CreateSubCategory(listlistWizard);
            var sub6 = CreateSubCategory(listlistShaman);

            var listMageSubcategories = new List<TalentLevelLeaf>();
            listMageSubcategories.Add(sub4);
            listMageSubcategories.Add(sub5);
            listMageSubcategories.Add(sub6);

            var mageTalents = new List<Talent>()
            {
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Mage1),
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Mage2)
            };

            var mageMainLevel = CreateMainLevel(mageTalents, listMageSubcategories);

            var sub7 = CreateSubCategory(listlistAssassin);
            var sub8 = CreateSubCategory(listlistBeastMaster);
            var sub9 = CreateSubCategory(listlistDruid);

            var listRangerSubcategories = new List<TalentLevelLeaf>();
            listRangerSubcategories.Add(sub7);
            listRangerSubcategories.Add(sub8);
            listRangerSubcategories.Add(sub9);

            var rangerTalents = new List<Talent>()
            {
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Ranger1),
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Ranger2)
            };

            var rangerMainlevel = CreateMainLevel(rangerTalents, listRangerSubcategories);

            var basicTalents = new List<Talent>()
            {
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.SuperAttack),
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Defend),
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Wait),
            };

            var mainLevelsList = new List<TalentLevelLeaf>();
            mainLevelsList.Add(fighterMainLevel);
            mainLevelsList.Add(mageMainLevel);
            mainLevelsList.Add(rangerMainlevel);

            var basicLevel = CreateMainLevel(basicTalents, mainLevelsList);
            var basicLevelsList = new List<TalentLevelLeaf>();
            basicLevelsList.Add(basicLevel);
            var beginningTalents = new List<Talent>()
            {
                _getTalent.FromEnum(Core.Abilities.Talents.Enum.TalentList.Attack)
            };

            var beginningLevel = CreateMainLevel(beginningTalents, basicLevelsList);
            foreach (var beginningLevelTalentNode in beginningLevel.TalentNodes)
            {
                beginningLevelTalentNode.IsUnlocked = true;
            }
            return new TalentTreeStructure(beginningLevel);
        }



        private static TalentLevelLeaf CreateMainLevel(List<Talent> listTalentNameFirst1, List<TalentLevelLeaf> listLevels)
        {
            var firstMainLevel = new TalentLevelLeaf();

            foreach (var s in listTalentNameFirst1)
            {
                firstMainLevel.TalentNodes.Add(new TalentNode(s));
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

        private static TalentLevelLeaf CreateSubCategory(List<List<Talent>> listListTalents)
        {
            TalentLevelLeaf level = null;
            for (var i = 0; i < listListTalents.Count; i++)
            {
                level = CreateMainLevel(listListTalents[0], new List<TalentLevelLeaf>() { level });
            }

            return level;
        }
    }
}
