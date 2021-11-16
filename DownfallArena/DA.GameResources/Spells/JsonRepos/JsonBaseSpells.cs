using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DA.Game.Resources.Spells
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
