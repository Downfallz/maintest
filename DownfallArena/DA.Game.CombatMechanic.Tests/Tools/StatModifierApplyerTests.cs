using DA.Game.CombatMechanic.Tools;
using System;
using DA.Game.CombatMechanic.Tests.Attributes;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.Domain.Models.TalentsManagement.Spells.Enum;
using Xunit;

namespace DA.Game.CombatMechanic.Tests.Tools
{
    public class StatModifierApplyerTests
    {
        [AutoMoqData]
        [Theory]
        public void GiveNullArgs_ApplyEffect_ThrowsExceptions(StatModifierApplyer sut, StatModifier rndStatModifier, Character rndChar)
        {
            Assert.Throws<ArgumentNullException>(() => sut.ApplyEffect(null, null));
            Assert.Throws<ArgumentNullException>(() => sut.ApplyEffect(rndStatModifier, null));
            Assert.Throws<ArgumentNullException>(() => sut.ApplyEffect(null, rndChar));
        }

        [AutoMoqData]
        [Theory]
        public void GivenDeadCharacter_ApplyEffect_ThrowsExceptions(StatModifierApplyer sut, StatModifier rndStatModifier, Character rndChar)
        {
            rndChar.Health = 0;
            Assert.Throws<Exception>(() => sut.ApplyEffect(rndStatModifier, rndChar));
        }

        [Theory]
        [InlineAutoMoqData(10, 3, 20, 13)]
        [InlineAutoMoqData(2, 2, 20, 20)]
        [InlineAutoMoqData(100, 3, 20, 0)]
        [InlineAutoMoqData(-10, 3, 20, 20)]
        [InlineAutoMoqData(5, 10, 1, 1)]
        public void GivenDmgSpell_ApplyEffect_ReducesHealth(int damage, int def, int startingHealth, int finishedHealth, Character rndChar, StatModifierApplyer sut)
        {
            StatModifier statMod = new StatModifier
            {
                Modifier = damage,
                StatType = Stats.Damage
            };

            rndChar.BonusDefense = def;
            rndChar.Health = startingHealth;

            sut.ApplyEffect(statMod, rndChar);

            Assert.Equal(finishedHealth, rndChar.Health);
        }

        [Theory]
        [InlineAutoMoqData(-500, 2, 0)]
        [InlineAutoMoqData(20, 0.2, 0.4)]
        [InlineAutoMoqData(50, 0, 0.5)]
        [InlineAutoMoqData(75, 0.5, 1)]
        public void GivenCritSpell_ApplyEffect_AugmentsCrit(int critValue, double startingCrit, double finishedCrit, Character rndChar, StatModifierApplyer sut)
        {
            StatModifier statMod = new StatModifier
            {
                Modifier = critValue,
                StatType = Stats.Critical
            };

            rndChar.BonusCritical = startingCrit;

            sut.ApplyEffect(statMod, rndChar);

            Assert.Equal(finishedCrit, rndChar.BonusCritical);
        }

        [Theory]
        [InlineAutoMoqData(-500, 2, 0)]
        [InlineAutoMoqData(2, 3, 5)]
        [InlineAutoMoqData(50, 0, 50)]
        [InlineAutoMoqData(-3, 5, 2)]
        public void GivenDefSpell_ApplyEffect_AugmentsDef(int defValue, int startingDef, int finishedDef, Character rndChar, StatModifierApplyer sut)
        {
            StatModifier statMod = new StatModifier
            {
                Modifier = defValue,
                StatType = Stats.Defense
            };

            rndChar.BonusDefense = startingDef;

            sut.ApplyEffect(statMod, rndChar);

            Assert.Equal(finishedDef, rndChar.BonusDefense);
        }

        [Theory]
        [InlineAutoMoqData(-500, 2, 0)]
        [InlineAutoMoqData(2, 3, 5)]
        [InlineAutoMoqData(50, 0, 50)]
        [InlineAutoMoqData(-3, 5, 2)]
        public void GivenEnergySpell_ApplyEffect_AugmentsEnergy(int engValue, int startingEng, int finishEng, Character rndChar, StatModifierApplyer sut)
        {
            StatModifier statMod = new StatModifier
            {
                Modifier = engValue,
                StatType = Stats.Energy
            };

            rndChar.Energy = startingEng;

            sut.ApplyEffect(statMod, rndChar);

            Assert.Equal(finishEng, rndChar.Energy);
        }

        [Theory]
        [InlineAutoMoqData(-500, 2, 1, 0)]
        [InlineAutoMoqData(2, 3, 3, 3)]
        [InlineAutoMoqData(2, 12, 14, 14)]
        [InlineAutoMoqData(222, 12, 14, 14)]
        [InlineAutoMoqData(3, 12, 166, 15)]
        public void GivenHpSpell_ApplyEffect_AugmentsHp(int hpValue, int startingHp, int baseHp, int finishHp, Character rndChar, StatModifierApplyer sut)
        {
            StatModifier statMod = new StatModifier
            {
                Modifier = hpValue,
                StatType = Stats.Health
            };

            rndChar.Health = startingHp;
            rndChar.BaseHealth = baseHp;

            sut.ApplyEffect(statMod, rndChar);

            Assert.Equal(finishHp, rndChar.Health);
        }

        [Theory]
        [InlineAutoMoqData(-500, 2, 0)]
        [InlineAutoMoqData(2, 3, 5)]
        [InlineAutoMoqData(50, 0, 50)]
        [InlineAutoMoqData(-3, 5, 2)]
        public void GivenInitSpell_ApplyEffect_AugmentsInit(int initValue, int startingInit, int finishInit, Character rndChar, StatModifierApplyer sut)
        {
            StatModifier statMod = new StatModifier
            {
                Modifier = initValue,
                StatType = Stats.Initiative
            };

            rndChar.BonusInitiative = startingInit;

            sut.ApplyEffect(statMod, rndChar);

            Assert.Equal(finishInit, rndChar.BonusInitiative);
        }

        [Theory]
        [InlineAutoMoqData(-500, 2, 0)]
        [InlineAutoMoqData(2, 3, 5)]
        [InlineAutoMoqData(50, 0, 50)]
        [InlineAutoMoqData(-3, 5, 2)]
        public void GivenExtraPointSpell_ApplyEffect_AugmentsExtraPoint(int value, int startingValue, int finishValue, Character rndChar, StatModifierApplyer sut)
        {
            StatModifier statMod = new StatModifier
            {
                Modifier = value,
                StatType = Stats.Minions
            };

            rndChar.ExtraPoint = startingValue;

            sut.ApplyEffect(statMod, rndChar);

            Assert.Equal(finishValue, rndChar.ExtraPoint);
        }

        [Theory]
        [InlineAutoMoqData(-500, 2, 0)]
        [InlineAutoMoqData(2, 3, 5)]
        [InlineAutoMoqData(50, 0, 50)]
        [InlineAutoMoqData(-3, 5, 2)]
        public void GivenRetaliateSpell_ApplyEffect_AugmentsRetaliate(int value, int startingValue, int finishValue, Character rndChar, StatModifierApplyer sut)
        {
            StatModifier statMod = new StatModifier
            {
                Modifier = value,
                StatType = Stats.Retaliate
            };

            rndChar.BonusRetaliate = startingValue;

            sut.ApplyEffect(statMod, rndChar);

            Assert.Equal(finishValue, rndChar.BonusRetaliate);
        }

        [Theory]
        [InlineAutoMoqData(-500)]
        [InlineAutoMoqData(2)]
        [InlineAutoMoqData(50)]
        [InlineAutoMoqData(-3)]
        public void GivenStunSpell_ApplyEffect_StunsChar(int value, Character rndChar, StatModifierApplyer sut)
        {
            StatModifier statMod = new StatModifier
            {
                Modifier = value,
                StatType = Stats.Stun
            };

            sut.ApplyEffect(statMod, rndChar);

            Assert.True(rndChar.IsStunned);
        }
    }
}