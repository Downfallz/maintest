using DA.Game.Resources.Generator;
using DA.Game.Resources.Spells;
using DA.Game.TalentsManagement.Tools;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using Xunit;

namespace DA.Game.Resources.Tests
{
    public class InitialTools
    {
        [Fact(Skip = "use at will")]
        public void CreateInitialJsonFiles()
        {
            List<Type> types = new List<Type>();
            types.Add(typeof(AssassinSpells));
            types.Add(typeof(BerserkerSpells));
            types.Add(typeof(BrawlerSpells));
            types.Add(typeof(CreatureSpells));
            types.Add(typeof(LeechSpells));
            types.Add(typeof(MercenarySpells));
            types.Add(typeof(NecromancerSpells));
            types.Add(typeof(ScoundrelSpells));
            types.Add(typeof(ShamanSpells));
            types.Add(typeof(SorcererSpells));
            types.Add(typeof(TricksterSpells));
            types.Add(typeof(WarlordSpells));
            types.Add(typeof(WizardSpells));

            foreach (var t in types)
            {
                List<Spell> spells = new List<Spell>();

                ConstructorInfo constructor = t.GetConstructor(Type.EmptyTypes);
                object inst = constructor.Invoke(new object[] { });

                foreach (var m in t.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance))
                {
                    spells.Add((Spell)m.Invoke(inst, new object[] { }));
                }

                string json = JsonConvert.SerializeObject(spells, new JsonSerializerSettings()
                {
                    Formatting = Formatting.Indented
                });

                using (StreamWriter w = new StreamWriter($@"C:\lol\{t.Name}.json"))
                {
                    w.WriteLine(json);
                    w.Flush();
                }
            }
        }
    }
}