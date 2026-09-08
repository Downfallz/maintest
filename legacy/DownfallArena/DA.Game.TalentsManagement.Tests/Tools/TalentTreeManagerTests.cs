using AutoFixture.Xunit2;
using DA.Game.TalentsManagement.Tools;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.TalentsManagement;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.TalentsManagement.Tests.Attributes;
using Xunit;

namespace DA.Game.TalentsManagement.Tests.Tools
{
    public class TalentTreeManagerTests
    {
        #region InitializeNewTalentTree

        [AutoMoqData]
        [Theory]
        public void InitializeCharacterTalentTree_CallsTalentTreeManagerInitializer([Frozen] Mock<ITalentTreeBuilder> manager, 
            TalentTreeManager sut)
        {
            TalentTreeStructure ts = sut.InitializeNewTalentTree();

            Assert.NotNull(ts);
            manager.Verify(x => x.GenerateNewTree(), Times.Once);
        }

        #endregion

        #region GetUnlockedSpells

        #endregion
        #region GetUnlockableSpells



        #endregion
        #region GetAllSpells



        #endregion
        #region UnlockSpell



        #endregion
    }
}