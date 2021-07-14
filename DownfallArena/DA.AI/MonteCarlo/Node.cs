using System;
using System.Collections.Generic;
using System.Linq;

namespace DA.AI.MonteCarlo
{
    public class Node
    {
        public Node()
        {
            ChildArray = new List<Node>();
        }
        public State State { get; set; }
        public Node Parent { get; set; }
        public List<Node> ChildArray { get; set; }

        public Node GetRandomChildNode()
        {
            Random rnd = new Random();
            int indexRnd = rnd.Next(0, ChildArray.Count);
            return ChildArray[indexRnd];
        }

        public Node GetChildWithMaxScore()
        {
            Random rnd = new Random();
            int indexRnd = rnd.Next(0, ChildArray.Count);
            return ChildArray.OrderByDescending(x => x.State.VisitCount).First();
        }

        public Node(Node node)
        {
            ChildArray = new List<Node>();
            State = new State(node.State);
            if (node.Parent != null)
                Parent = node.Parent;
            List<Node> childArray = node.ChildArray;
            foreach (Node child in childArray)
            {
                ChildArray.Add(new Node(child));
            }
        }
    }
}
