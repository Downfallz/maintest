using DA.Game.Resources.Spells;

namespace DA.Game.Resources.Generator
{
    public class ResourceContext : IResourceContext
    {
        public ResourceContext(IAssassinSpells assassinSpells,
            IBerserkerSpells berserkerSpells,
            IBrawlerSpells brawlerSpells,
            ICreatureSpells creatureSpells,
            ILeechSpells leechSpells,
            IMercenarySpells mercenarySpells,
            INecromancerSpells necromancerSpells,
            IScoundrelSpells scoundrelSpells,
            IShamanSpells shamanSpells,
            ISorcererSpells sorcererSpells,
            ITricksterSpells tricksterSpells,
            IWarlordSpells warlordSpells,
            IWizardSpells wizardSpells)
        {
            AssassinSpells = assassinSpells;
            BerserkerSpells = berserkerSpells;
            BrawlerSpells = brawlerSpells;
            CreatureSpells = creatureSpells;
            LeechSpells = leechSpells;
            MercenarySpells = mercenarySpells;
            NecromancerSpells = necromancerSpells;
            ScoundrelSpells = scoundrelSpells;
            ShamanSpells = shamanSpells;
            SorcererSpells = sorcererSpells;
            TricksterSpells = tricksterSpells;
            WarlordSpells = warlordSpells;
            WizardSpells = wizardSpells;
        }

        public IAssassinSpells AssassinSpells { get; }
        public IBerserkerSpells BerserkerSpells { get; }
        public IBrawlerSpells BrawlerSpells { get; }
        public ICreatureSpells CreatureSpells { get; }
        public ILeechSpells LeechSpells { get; }
        public IMercenarySpells MercenarySpells { get; }
        public INecromancerSpells NecromancerSpells { get; }
        public IScoundrelSpells ScoundrelSpells { get; }
        public IShamanSpells ShamanSpells { get; }
        public ISorcererSpells SorcererSpells { get; }
        public ITricksterSpells TricksterSpells { get; }
        public IWarlordSpells WarlordSpells { get; }
        public IWizardSpells WizardSpells { get; }
    }
}
