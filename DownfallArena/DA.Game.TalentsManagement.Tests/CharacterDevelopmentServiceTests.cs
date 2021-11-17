using AutoFixture.Xunit2;
using DA.Game.TalentsManagement.Tools;
using Moq;
using System;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.TalentsManagement;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.TalentsManagement.Tests.Attributes;
using Xunit;

namespace DA.Game.TalentsManagement.Tests
{
    public class CharacterDevelopmentServiceTests
    {
        #region InitializeNewCharacter
        [AutoMoqData]
        [Theory]
        public void InitializeNewCharacter_ReturnsWellFormedCharacter(CharacterDevelopmentService sut)
        {
            Character newCharacter = sut.InitializeNewCharacter();

            Assert.NotNull(newCharacter);
            Assert.NotNull(newCharacter.CharacterTalentStats);
            Assert.NotNull(newCharacter.TalentTreeStructure);
            Assert.Equal(20, newCharacter.BaseHealth);
            Assert.Equal(0, newCharacter.BonusAttackPower);
            Assert.Equal(0, newCharacter.BonusCritical);
            Assert.Equal(0, newCharacter.BonusDefense);
            Assert.Equal(0, newCharacter.BonusInitiative);
            Assert.Equal(0, newCharacter.BonusRetaliate);
            Assert.Equal(0, newCharacter.Energy);
            Assert.Equal(0, newCharacter.ExtraPoint);
            Assert.Equal(20, newCharacter.Health);
            Assert.Equal(0, newCharacter.Initiative);
            Assert.False(newCharacter.IsStunned);
            Assert.False(newCharacter.IsDead);
            Assert.Empty(newCharacter.CharConditions);
            Assert.NotEqual(Guid.Empty, newCharacter.Id);
            Assert.NotNull(newCharacter.Name);
            Assert.NotEqual("", newCharacter.Name);
            Assert.Equal(0, newCharacter.TeamNumber);
        }

        [AutoMoqData]
        [Theory]
        public void InitializeNewCharacter_CallsUnderlyingDependences([Frozen] Mock<ICharacterTalentStatsHandler> charTalentStats,
            [Frozen] Mock<ITalentTreeManager> talentTreeManager,
            TalentTreeStructure structure,
            CharacterDevelopmentService sut)
        {
            talentTreeManager.Setup(x => x.InitializeNewTalentTree()).Returns(structure);
            Character newCharacter = sut.InitializeNewCharacter();

            talentTreeManager.Verify(x => x.InitializeNewTalentTree(), Times.Once);
            charTalentStats.Verify(
                x => x.UpdateCharTalentTree(It.Is<TalentTreeStructure>(x => object.ReferenceEquals(x, structure))),
                Times.Once);
        }
        #endregion

        #region UnlockSpell
        [AutoMoqData]
        [Theory]
        public void GiveNullArgs_UnlockSpell_ThrowsExceptions(CharacterDevelopmentService sut, Character rndChar)
        {
            Assert.Throws<ArgumentNullException>(() => sut.UnlockSpell(null, null));
            Assert.Throws<ArgumentNullException>(() => sut.UnlockSpell(null, new Spell()));
            Assert.Throws<ArgumentException>(() => sut.UnlockSpell(new Character(), new Spell()));
            Assert.Throws<ArgumentNullException>(() => sut.UnlockSpell(rndChar, null));
        }

        [AutoMoqData]
        [Theory]
        public void GivenAnyCharAndSpell_UnlockSpell_ChangesCharStats(
            [Frozen] Mock<ICharacterTalentStatsHandler> mockCharTalentStats,
            CharacterTalentStats stats,
            Character rndChar,
            Spell spell,
            CharacterDevelopmentService sut)
        {
            mockCharTalentStats.Setup(x => x.UnlockSpell(It.IsAny<TalentTreeStructure>(), It.IsAny<Spell>()))
                .Returns(stats);

            sut.UnlockSpell(rndChar, spell);

            Assert.Same(stats, rndChar.CharacterTalentStats);
        }
        #endregion
    }
}