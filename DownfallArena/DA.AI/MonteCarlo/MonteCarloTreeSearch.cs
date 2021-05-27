using System;
using System.Collections.Generic;
using DA.Game;
using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Services;

namespace DA.AI.MonteCarlo
{
    public class MonteCarloTreeSearch
    {
        static int WIN_SCORE = 10;
        int Level { get; set; }
        int opponent;
        private readonly IBattleEngine _be;

        public MonteCarloTreeSearch(IBattleEngine be)
        {
            Level = 3;
            _be = be;
        }

        private int GetMillisForCurrentLevel()
        {
            return 2 * (Level - 1) + 1;
        }

        public Battle FindNextMove(Battle board, int playerNo)
        {
            DateTime start = DateTime.Now;
            DateTime end = start.AddMilliseconds(2000 * GetMillisForCurrentLevel());
            // define an end time which will act as a terminating condition

            opponent = 3 - playerNo;
            Tree tree = new Tree();
            tree.Root = new Node();
            Node rootNode = tree.Root;
            rootNode.State = new State();
            rootNode.State.Board = board;
            rootNode.State.PlayerNo = opponent;

            while (DateTime.Now < end)
            {
                Node promisingNode = selectPromisingNode(rootNode);
                if (promisingNode.State.Board.Winner == -1)
                {
                    expandNode(promisingNode);
                }
                Node nodeToExplore = promisingNode;
                if (promisingNode.ChildArray.Count > 0)
                {
                    nodeToExplore = promisingNode.GetRandomChildNode();
                }
                int playoutResult = SimulateRandomPlayout(nodeToExplore);
                BackPropogation(nodeToExplore, playoutResult);
            }

            Node winnerNode = rootNode.GetChildWithMaxScore();
            tree.Root = winnerNode;
            return winnerNode.State.Board;
        }

        private Node selectPromisingNode(Node rootNode)
        {
            Node node = rootNode;
            while (node.ChildArray.Count != 0)
            {
                node = UCT.findBestNodeWithUCT(node);
            }
            return node;
        }

        private void expandNode(Node node)
        {
            List<State> possibleStates = node.State.GetAllPossibleStates(_be);
            foreach(var s in possibleStates)
            {
                Node newNode = new Node();
                newNode.State = s;
                newNode.Parent = node;
                node.ChildArray.Add(newNode);
            }
        }

        private void BackPropogation(Node nodeToExplore, int playerNo)
        {
            Node tempNode = nodeToExplore;
            while (tempNode != null)
            {
                tempNode.State.IncrementVisit();
                if (tempNode.State.PlayerNo == playerNo)
                {
                    tempNode.State.AddScore(WIN_SCORE);
                }
                tempNode = tempNode.Parent;
            }
        }
        private int SimulateRandomPlayout(Node node)
        {
            Node tempNode = new Node(node);
            State tempState = tempNode.State;
            int boardStatus = tempState.Board.Winner;
            if (tempState.Board.Winner == opponent)
            {
                tempNode.Parent.State.Score = int.MinValue;
                return tempState.Board.Winner;
            }
            while (tempState.Board.Winner == -1)
            {
                tempState.RandomPlay(_be);
                boardStatus = tempState.Board.Winner;
            }
            return boardStatus;
        }
    }
}
