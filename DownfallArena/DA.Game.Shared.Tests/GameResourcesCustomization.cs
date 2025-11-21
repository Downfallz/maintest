using AutoFixture;
using DA.Game.Shared.Contracts.Resources.Creatures;
using DA.Game.Shared.Contracts.Resources.Spells;

namespace DA.Game.Shared.Tests;

internal sealed class GameResourcesCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        // SpellId always valid: spell:<name>:v<version>
        fixture.Register(() =>
        {
            var baseName = fixture.Create<string>() ?? "spell";
            baseName = baseName.Replace(" ", "_").ToUpperInvariant();

            var version = fixture.Create<int>();
            if (version <= 0) version = 1;

            var raw = TestIdFactory.CreateSpellIdString(baseName, version);
            return SpellId.New(raw);
        });

        // CharacterDefId always valid: creature:<name>:v<version>
        fixture.Register(() =>
        {
            var baseName = fixture.Create<string>() ?? "creature";
            baseName = baseName.Replace(" ", "_").ToUpperInvariant();

            var version = fixture.Create<int>();
            if (version <= 0) version = 1;

            var raw = TestIdFactory.CreateCharacterDefIdString(baseName, version);
            return CharacterDefId.New(raw);
        });
    }
}
