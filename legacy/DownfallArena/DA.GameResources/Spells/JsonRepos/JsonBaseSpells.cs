using System.Collections.Generic;
using System.IO;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using Newtonsoft.Json;

namespace DA.Game.Resources.Spells.JsonRepos
{
    public class JsonBaseSpells
    {
        private readonly string fileName;

        public JsonBaseSpells(string fileName)
        {
            this.fileName = fileName;
        }
        protected List<Spell> GetList()
        {
            StreamReader r = new StreamReader($"Spells/JsonRepos/Files/{fileName}.json");
            string jsonString = r.ReadToEnd();
            List<Spell> list = JsonConvert.DeserializeObject<List<Spell>>(jsonString);
            return list;
        }
    }
}
