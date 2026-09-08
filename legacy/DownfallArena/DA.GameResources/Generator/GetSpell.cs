using DA.Game.TalentsManagement.Tools;
using System;
using DA.Game.Domain.Models.TalentsManagement.Enum;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Generator
{
    public class GetSpell : IGetSpell
    {
        public GetSpell(IResourceContext resourceContext)
        {
            ResourceContext = resourceContext;
        }

        public IResourceContext ResourceContext { get; }

        public Spell FromEnum(TalentList colorBand) =>
            colorBand switch
            {
                TalentList.Wait => ResourceContext.CreatureSpells.GetWait(),
                TalentList.Strike => ResourceContext.CreatureSpells.GetAttack(),
                TalentList.HeavyStrike => ResourceContext.CreatureSpells.GetSuperAttack(),
                TalentList.Pummel => ResourceContext.BrawlerSpells.GetPummel(),
                TalentList.Guard => ResourceContext.BrawlerSpells.GetGuard(),
                TalentList.EnragedCharge => ResourceContext.BerserkerSpells.GetEnragedCharge(),
                TalentList.Tornado => ResourceContext.BerserkerSpells.GetTornado(),
                TalentList.PsychoRush => ResourceContext.BerserkerSpells.GetPsychoRush(),
                TalentList.ProtectiveSlam => ResourceContext.MercenarySpells.GetProtectiveSlam(),
                TalentList.ChainSlash => ResourceContext.MercenarySpells.GetChainSlash(),
                TalentList.ThunderingSeal => ResourceContext.MercenarySpells.GetThunderingSeal(),
                TalentList.FullPlate => ResourceContext.WarlordSpells.GetFullPlate(),
                TalentList.CrushingStomp => ResourceContext.WarlordSpells.GetCrushingStomp(),
                TalentList.RestorativeGush => ResourceContext.WarlordSpells.GetRestorativeGush(),
                TalentList.LightningBolt => ResourceContext.SorcererSpells.GetLightningBolt(),
                TalentList.Rejuvenate => ResourceContext.SorcererSpells.GetRejuvenate(),
                TalentList.Meteor => ResourceContext.WizardSpells.GetMeteor(),
                TalentList.IceSpear => ResourceContext.WizardSpells.GetIceSpear(),
                TalentList.EngulfingFlames => ResourceContext.WizardSpells.GetEngulfingFlames(),
                TalentList.SummonMinions => ResourceContext.NecromancerSpells.GetSummonMinions(),
                TalentList.RevenantGuards => ResourceContext.NecromancerSpells.GetRevenantGuards(),
                TalentList.CrazedSpecters => ResourceContext.NecromancerSpells.GetCrazedSpecters(),
                TalentList.HealingScreech => ResourceContext.ShamanSpells.GetHealingScreech(),
                TalentList.ToxicWaves => ResourceContext.ShamanSpells.GetToxicWaves(),
                TalentList.RestoringBurst => ResourceContext.ShamanSpells.GetRestoringBurst(),
                TalentList.PoisonSlash => ResourceContext.ScoundrelSpells.GetPoisonSlash(),
                TalentList.ThrowingStar => ResourceContext.ScoundrelSpells.GetThrowingStar(),
                TalentList.ParasiteJab => ResourceContext.LeechSpells.GetParasiteJab(),
                TalentList.HatefulSacrifice => ResourceContext.LeechSpells.GetHatefulSacrifice(),
                TalentList.SoulDevourer => ResourceContext.LeechSpells.GetSoulDevourer(),
                TalentList.Momentum => ResourceContext.AssassinSpells.GetMomentum(),
                TalentList.DeathSquad => ResourceContext.AssassinSpells.GetDeathSquad(),
                TalentList.MortalWound => ResourceContext.AssassinSpells.GetMortalWound(),
                TalentList.NoxiousCure => ResourceContext.TricksterSpells.GetNoxiousCure(),
                TalentList.TranquilizerDart => ResourceContext.TricksterSpells.GetTranquilizerDart(),
                TalentList.InfectiousBlast => ResourceContext.TricksterSpells.GetInfectiousBlast(),
                _ => throw new ArgumentException(message: "invalid enum value", paramName: nameof(colorBand)),
            };

    }
}
