//#define EVAL_DEBUG
#define UNPROD_LIST

using System;
using System.Collections.Generic;

namespace ICSimulator
{
    /// <summary>Generates permutations, obtained from http://www.interact-sw.co.uk/iangblog/2004/09/16/permuterate</summary>
    public class PermuteUtils
    {
        // Returns an enumeration of enumerators, one for each permutation
        // of the input.
        public static IEnumerable<IEnumerable<T>> Permute<T>(IEnumerable<T> list, int count)
        {
            if (count == 0)
            {
                yield return new T[0];
            }
            else
            {
                int startingElementIndex = 0;
                foreach (T startingElement in list)
                {
                    IEnumerable<T> remainingItems = AllExcept(list, startingElementIndex);

                    foreach (IEnumerable<T> permutationOfRemainder in Permute(remainingItems, count - 1))
                    {
                        yield return Concat<T>(
                            new T[] { startingElement },
                            permutationOfRemainder);
                    }
                    startingElementIndex += 1;
                }
            }
        }

        // Enumerates over contents of both lists.
        public static IEnumerable<T> Concat<T>(IEnumerable<T> a, IEnumerable<T> b)
        {
            foreach (T item in a) { yield return item; }
            foreach (T item in b) { yield return item; }
        }

        // Enumerates over all items in the input, skipping over the item
        // with the specified offset.
        public static IEnumerable<T> AllExcept<T>(IEnumerable<T> input, int indexToSkip)
        {
            int index = 0;
            foreach (T item in input)
            {
                if (index != indexToSkip) yield return item;
                index += 1;
            }
        }
    }

    public class ExhaustiveSweep<T>
    {
        List<T[]> SweptVariables = new List<T[]>();
        public void addParameter(T[] values)
        {
            SweptVariables.Add(values);
        }

        public ulong Count
        {
            get
            {
                ulong maxConfigs = 1;
                foreach (T[] param in SweptVariables)
                    maxConfigs *= (ulong)param.Length;
                return maxConfigs;
            }
        }

        public T[] GetConfig(ulong index)
        {
            T[] config = new T[SweptVariables.Count];

            for (int i = 0; i < SweptVariables.Count; i++)
            {
                ulong length = (ulong)SweptVariables[i].Length;

                config[i] = SweptVariables[i][(int)(index % length)];
                index /= length;
            }
            return config;
        }
    }

    public class SimpleNode : Node
    {
        public SimpleNode(NodeMapping n, Coord c) : base(n, c) { }
        private Flit receivedFlit = null;
        public override void receiveFlit(Flit f)
        {
            receivedFlit = f;
        }
        public Flit getReceivedFlit()
        {
            Flit f = receivedFlit;
            receivedFlit = null;
            return f;
        }
    }

    public class RouterEval
    {

        private static int[] possibleDests = { -1, 0, 1, 2, 3, 4, 5, 6, 7, 8 };
#if EVAL_DEBUG
        static int[] possibleDirs = { Simulator.DIR_UP, Simulator.DIR_DOWN, Simulator.DIR_LEFT, Simulator.DIR_RIGHT, Simulator.DIR_NONE };
        private static string[] desiredDirs = { "UL", "U", "UR", "L", ".", "R", "DL", "D", "DR" };
        //private static string[] desiredDirs = { "XX", "03", "0", "01", "3", ".", "1", "23", "2", "21" };

        private static int[] simpleDests = { 2 };
        private static string[] cardinalDirections = { "N", "E", "S", "W" };
        private static string[] directions = { "U", "R", "D", "L", "." };
#endif

        public static void simplePrint(int dir, Flit f)
        {
#if EVAL_DEBUG
            Console.Write("\t{0,2}: ", directions[dir]);
            if (f != null)
                Console.Write("{0} Wants {1,2}", f.packet.ID % 8, desiredDirs[f.packet.dest.ID], f.packet.dest.ID);
            else
                Console.Write(" Null flit");
#endif
        }

        public static void evaluate()
        {
            //Require 3x3 for assumptions in Coord labeling
            if (Config.network_nrX != 3 || Config.network_nrY != 3)
                throw new Exception("Incorrect network size for router evaluation!");

            Router theRouter = Network.MakeRouter(new Coord(1, 1));
            SimpleNode node = new SimpleNode(Simulator.network.nodes[0].mapping, theRouter.coord);
            theRouter.setNode(node);

            for (int i = 0; i < 4; i++)
            {
                Link inLink = new Link(0);
                Link outLink = new Link(0);

                theRouter.linkIn[i] = inLink;
                theRouter.linkOut[i] = outLink;
            }

            //FINISH CONFIGURING LINKS

            ExhaustiveSweep<int> s = new ExhaustiveSweep<int>();
            s.addParameter(possibleDests);
            s.addParameter(possibleDests);
            s.addParameter(possibleDests);
            s.addParameter(possibleDests);
            //s.addParameter("L", possibleDests);

            ulong timesToRunEach = 1;

            Console.WriteLine("iterating over {0}(*{1}) configurations", s.Count, timesToRunEach);
            for (ulong i = 0; i < Config.RouterEvaluationIterations * s.Count; i++)
            {
                if (i % 4096 == 0)
                    Console.WriteLine("Completed {0} evalutations", i);

                int[] nextConfig = s.GetConfig(i / Config.RouterEvaluationIterations);
#if EVAL_DEBUG
                Console.WriteLine("i{0} N{1} S{2} E{3} W{4}", i, nextConfig[0], nextConfig[1], nextConfig[2], nextConfig[3]);
#endif
                // assign each config direction to its link with a new packet/flit
                for (int j = 0; j < 4; j++)
                {
                    int dest = nextConfig[j];
                    Flit incomingFlit = null;
                    if (dest >= 0)
                    {
                        incomingFlit = new Packet(null, 0, 1, new Coord(1, 1), new Coord(nextConfig[j])).flits[0];
                        incomingFlit.packet.creationTime = (ulong)Simulator.rand.Next(4);
                    }
                    theRouter.linkIn[j].In = incomingFlit;
                    theRouter.linkIn[j].doStep();

                    simplePrint(j, incomingFlit);
                }
#if EVAL_DEBUG
                Console.WriteLine();
#endif
                theRouter.doStep();

                for (int j = 0; j < 4; j++)
                {
                    theRouter.linkOut[j].doStep();
                    simplePrint(j, theRouter.linkOut[j].Out);
                }
                simplePrint(4, node.getReceivedFlit());
#if EVAL_DEBUG
                Console.WriteLine();
#endif
            }
        }
    }
}
