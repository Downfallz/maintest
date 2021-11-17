using AutoFixture.Xunit2;
using DA.Game.CombatMechanic.Tools;
using Moq;
using System;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.Domain.Models.TalentsManagement.Spells.Enum;
using Xunit;

namespace DA.Game.CombatMechanic.Tests
{
    public class CharacterCondServiceTests
    {
        [AutoMoqData]
        [Theory]
        public void GiveNullArgs_ApplyCondition_ThrowsExceptions(CharacterCondService sut,
            CharCondition rndCharCond,
            Character rndCharacter)
        {
            Assert.Throws<ArgumentNullException>(() => sut.ApplyCondition(null, null));
            Assert.Throws<ArgumentNullException>(() => sut.ApplyCondition(rndCharCond, null));
            Assert.Throws<ArgumentNullException>(() => sut.ApplyCondition(null, rndCharacter));
            Assert.Throws<ArgumentException>(() => sut.ApplyCondition(new CharCondition(), rndCharacter));
        }

        [Theory]
        [InlineAutoMoqData(Stats.Damage)]
        [InlineAutoMoqData(Stats.Health)]
        [InlineAutoMoqData(Stats.Energy)]
        public void GivenRecurringStats_ApplyCondition_ApplyEffect(Stats stats,
            [Frozen] Mock<IStatModifierApplyer> manager,
    CharacterCondService sut,
    StatModifier statModifier,
    Character rndCharacter)
        {
            statModifier.StatType = stats;
            CharCondition charCond = new CharCondition
            {
                StatModifier = statModifier,
                RoundsLeft = 2,
                IsPermanent = true
            };

            sut.ApplyCondition(charCond, rndCharacter);

            manager.Verify(x => x.ApplyEffect(
                It.Is<StatModifier>(z => object.ReferenceEquals(z, statModifier)),
                It.Is<Character>(z => object.ReferenceEquals(z, rndCharacter))), Times.Once);
        }

        [Theory]
        [InlineAutoMoqData(Stats.Critical)]
        [InlineAutoMoqData(Stats.Defense)]
        [InlineAutoMoqData(Stats.Initiative)]
        [InlineAutoMoqData(Stats.Minions)]
        [InlineAutoMoqData(Stats.Retaliate)]
        [InlineAutoMoqData(Stats.Stun)]
        public void GivenNonRecurringStats_ApplyCondition_DontApplyEffect(Stats stats,
            [Frozen] Mock<IStatModifierApplyer> manager,
    CharacterCondService sut,
    StatModifier statModifier,
    Character rndCharacter)
        {
            statModifier.StatType = stats;
            CharCondition charCond = new CharCondition
            {
                StatModifier = statModifier,
                RoundsLeft = 2,
                IsPermanent = true
            };

            sut.ApplyCondition(charCond, rndCharacter);

            manager.Verify(x => x.ApplyEffect(
                It.Is<StatModifier>(z => object.ReferenceEquals(z, statModifier)),
                It.Is<Character>(z => object.ReferenceEquals(z, rndCharacter))), Times.Never);
        }

        [Theory]
        [AutoMoqData]
        public void GivenAnyCond_ApplyCondition_DecreaseRoundCount(
            [Frozen] Mock<IStatModifierApplyer> manager,
    CharacterCondService sut,
    CharCondition charCondition,
    Character rndCharacter)
        {
            int currentRoundLeft = charCondition.RoundsLeft;
            sut.ApplyCondition(charCondition, rndCharacter);
            Assert.Equal(currentRoundLeft - 1, charCondition.RoundsLeft);
        }
    }
}