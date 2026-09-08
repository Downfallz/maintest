using System;

namespace DA.Core.Abilities.Talents.Exceptions
{
    public class TalentException : Exception
    {
        public TalentException(string msg) : base(msg) {}
    }
}
