using System.Linq;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells.JsonRepos
{
    public class JsonTricksterSpells : JsonBaseSpells, ITricksterSpells
    {
        public JsonTricksterSpells(string fileName) : base(fileName)
        {
        }

        public Spell GetNoxiousCure()
        {
            return GetList().SingleOrDefault(x => x.Name == "Noxious Cure");
        }

        public Spell GetTranquilizerDart()
        {
            return GetList().SingleOrDefault(x => x.Name == "Tranquilizer Dart");
        }

        public Spell GetInfectiousBlast()
        {
            return GetList().SingleOrDefault(x => x.Name == "Infectious Blast");
        }
    }
}
