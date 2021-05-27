using System;
using DA.Core.Abilities.Main.Generator;
using DA.Core.Abilities.Talents;
using DA.Core.Abilities.Talents.Abstractions;
using DA.Core.Abilities.Talents.Enum;
using DA.Core.Abilities.Talents.Models;

namespace DA.Core.Abilities.Main
{
    public class GetTalent : IGetTalent
    {
        public Talent FromEnum(TalentList colorBand) =>
            colorBand switch
            {
                TalentList.Attack => Basic.GetAttack(),
                TalentList.SuperAttack => Basic.GetSuperAttack(),
                TalentList.Defend => Basic.GetDefend(),
                TalentList.Wait => Basic.GetWait(),
                TalentList.FighterAttack1 => Fighter.Get1(),
                TalentList.FighterAttack2 => Fighter.Get2(),
                TalentList.Berserker1 => Berserker.Get1(),
                TalentList.Berserker2 => Berserker.Get2(),
                TalentList.Berserker3 => Berserker.Get3(),
                TalentList.Berserker4 => Berserker.Get4(),
                TalentList.Soldier1 => Soldier.Get1(),
                TalentList.Soldier2 => Soldier.Get2(),
                TalentList.Soldier3 => Soldier.Get3(),
                TalentList.Soldier4 => Soldier.Get4(),
                TalentList.Paladin1 => Paladin.Get1(),
                TalentList.Paladin2 => Paladin.Get2(),
                TalentList.Paladin3 => Paladin.Get3(),
                TalentList.Paladin4 => Paladin.Get4(),
                TalentList.Mage1 => Mage.Get1(),
                TalentList.Mage2 => Mage.Get2(),
                TalentList.Warlock1 => Warlock.Get1(),
                TalentList.Warlock2 => Warlock.Get2(),
                TalentList.Warlock3 => Warlock.Get3(),
                TalentList.Warlock4 => Warlock.Get4(),
                TalentList.Wizard1 => Wizard.Get1(),
                TalentList.Wizard2 => Wizard.Get2(),
                TalentList.Wizard3 => Wizard.Get3(),
                TalentList.Wizard4 => Wizard.Get4(),
                TalentList.Shaman1 => Shaman.Get1(),
                TalentList.Shaman2 => Shaman.Get2(),
                TalentList.Shaman3 => Shaman.Get3(),
                TalentList.Shaman4 => Shaman.Get4(),
                TalentList.Ranger1 => Ranger.Get1(),
                TalentList.Ranger2 => Ranger.Get2(),
                TalentList.Assassin1 => Assassin.Get1(),
                TalentList.Assassin2 => Assassin.Get2(),
                TalentList.Assassin3 => Assassin.Get3(),
                TalentList.Assassin4 => Assassin.Get4(),
                TalentList.BeastMaster1 => BeastMaster.Get1(),
                TalentList.BeastMaster2 => BeastMaster.Get2(),
                TalentList.BeastMaster3 => BeastMaster.Get3(),
                TalentList.BeastMaster4 => BeastMaster.Get4(),
                TalentList.Druid1 => Druid.Get1(),
                TalentList.Druid2 => Druid.Get2(),
                TalentList.Druid3 => Druid.Get3(),
                TalentList.Druid4 => Druid.Get4(),
                _ => throw new ArgumentException(message: "invalid enum value", paramName: nameof(colorBand)),
            };

    }
}
