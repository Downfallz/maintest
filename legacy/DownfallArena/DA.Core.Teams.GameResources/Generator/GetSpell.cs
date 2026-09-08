using System;
using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Talents.Talents.Enum;
using DA.Core.Teams.Abstractions;
using DA.Core.Teams.GameResources.Data;

namespace DA.Core.Teams.GameResources.Generator
{
    public class GetSpell : IGetSpell
    {
        public Spell FromEnum(TalentList colorBand) =>
            colorBand switch
            {
                TalentList.Wait => Creature.GetWait(),
                TalentList.Strike => Creature.GetAttack(),
                TalentList.HeavyStrike => Creature.GetSuperAttack(),
                TalentList.Pummel => Brawler.GetPummel(),
                TalentList.Guard => Brawler.GetGuard(),
                TalentList.EnragedCharge => Berserker.GetEnragedCharge(),
                TalentList.Tornado => Berserker.GetTornado(),
                TalentList.PsychoRush => Berserker.GetPsychoRush(),
                TalentList.ProtectiveSlam => Mercenary.GetProtectiveSlam(),
                TalentList.ChainSlash => Mercenary.GetChainSlash(),
                TalentList.ThunderingSeal => Mercenary.GetThunderingSeal(),
                TalentList.FullPlate => Warlord.GetFullPlate(),
                TalentList.CrushingStomp => Warlord.GetCrushingStomp(),
                TalentList.RestorativeGush => Warlord.GetRestorativeGush(),
                TalentList.LightningBolt => Sorcerer.GetLightningBolt(),
                TalentList.Rejuvenate => Sorcerer.GetRejuvenate(),
                TalentList.Meteor => Wizard.GetMeteor(),
                TalentList.IceSpear => Wizard.GetIceSpear(),
                TalentList.EngulfingFlames => Wizard.GetEngulfingFlames(),
                TalentList.SummonMinions => Necromancer.GetSummonMinions(),
                TalentList.RevenantGuards => Necromancer.GetRevenantGuards(),
                TalentList.CrazedSpecters => Necromancer.GetCrazedSpecters(),
                TalentList.HealingScreech => Shaman.GetHealingScreech(),
                TalentList.ToxicWaves => Shaman.GetToxicWaves(),
                TalentList.RestoringBurst => Shaman.GetRestoringBurst(),
                TalentList.PoisonSlash => Scoundrel.GetPoisonSlash(),
                TalentList.ThrowingStar => Scoundrel.GetThrowingStar(),
                TalentList.ParasiteJab => Leech.GetParasiteJab(),
                TalentList.HatefulSacrifice => Leech.GetHatefulSacrifice(),
                TalentList.SoulDevourer => Leech.GetSoulDevourer(),
                TalentList.Momentum => Assassin.GetMomentum(),
                TalentList.DeathSquad => Assassin.GetDeathSquad(),
                TalentList.MortalWound => Assassin.GetMortalWound(),
                TalentList.NoxiousCure => Trickster.GetNoxiousCure(),
                TalentList.TranquilizerDart => Trickster.GetTranquilizerDart(),
                TalentList.InfectiousBlast => Trickster.GetInfectiousBlast(),
                _ => throw new ArgumentException(message: "invalid enum value", paramName: nameof(colorBand)),
            };

    }
}
