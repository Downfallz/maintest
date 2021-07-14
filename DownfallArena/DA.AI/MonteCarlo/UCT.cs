using System;
using System.Linq;

namespace DA.AI.MonteCarlo
{
    public class UCT
    {
        public static double uctValue(
          int totalVisit, double nodeWinScore, int nodeVisit)
        {
            if (nodeVisit == 0)
            {
                return int.MaxValue;
            }
            return ((double)nodeWinScore / (double)nodeVisit)
              + 1.41 * Math.Sqrt(Math.Log(totalVisit) / (double)nodeVisit);
        }

        public static Node findBestNodeWithUCT(Node node)
        {
            int parentVisit = node.State.VisitCount;
            Node a = node.ChildArray.OrderByDescending(x => uctValue(parentVisit, x.State.Score, x.State.VisitCount)).First();
            return a;
        }
    }
}
