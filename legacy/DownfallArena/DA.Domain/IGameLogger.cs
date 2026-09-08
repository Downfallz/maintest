using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain
{
    public interface IGameLogger
    {
        void Log(string message);
    }
}
