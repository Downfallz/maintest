using System;
using System.Linq;
using DA.Abilities;
using Xunit;

namespace DA.Spells.Tests
{
    public class SpellBookTests
    {
        [Fact]
        public void GivenNewTree()
        {
            var treeInitiatlizer = new TalentTreeInitializer(new GetTalent());
            var newTree = treeInitiatlizer.GenerateNewTree();

            var spellBook = new SpellBook(newTree);
            var listeSpells = spellBook.GetAllSpells();

            var listeUnlockable = spellBook.GetUnlockableSpells();
            spellBook.UnlockSpell(listeUnlockable[0]);
            var listeUnlockable2 = spellBook.GetUnlockableSpells();
            var listeunlocked = spellBook.GetUnlockedSpells();

            spellBook.UnlockSpell(listeSpells.Last());

        }
    }
}
