using DA.Game.Resources.Spells;

namespace DA.Game.Resources.Generator
{
    public interface IResourceContext
    {
        IAssassinSpells AssassinSpells { get; }
        IBerserkerSpells BerserkerSpells { get; }
        IBrawlerSpells BrawlerSpells { get; }
        ICreatureSpells CreatureSpells { get; }
        ILeechSpells LeechSpells { get; }
        IMercenarySpells MercenarySpells { get; }
        INecromancerSpells NecromancerSpells { get; }
        IScoundrelSpells ScoundrelSpells { get; }
        IShamanSpells ShamanSpells { get; }
        ISorcererSpells SorcererSpells { get; }
        ITricksterSpells TricksterSpells { get; }
        IWarlordSpells WarlordSpells { get; }
        IWizardSpells WizardSpells { get; }
    }
}