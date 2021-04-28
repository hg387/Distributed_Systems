using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Actor;
using LamportLock.Nodes;
using LamportLock.Utilities;

namespace LamportLock
{
    class Program
    {
        
        static async Task Main(string[] args)
        {
            int numberOfNodes = 0;
            if (args.Length == 1)
            {
                try
                {
                    numberOfNodes = Int32.Parse(args[0]);
                }
                catch (FormatException)
                {
                    Console.WriteLine("Unable to parse the passed argument, Exiting......");
                    return;
                }
            }
            else
            {
                Console.WriteLine("Arguments numbers are not correct, Exiting......");
                return;
            }

            ConsoleKeyInfo cki;
            Console.WriteLine("Starting the Lamport Lock");
            var sys = ActorSystem.Create("system");

            List<IActorRef> nodes = new List<IActorRef>();

            for (int i=0; i<numberOfNodes; i++)
            {
                IActorRef tmpNode = sys.ActorOf(Props.Create<Node>(i), $"node{i}");
                nodes.Add(tmpNode);
            }

            Initialize.otherNodes = nodes;
            Initialize.setPQueue();

            foreach (IActorRef node in nodes)
            {
                node.Tell(new Initialize());
            }

            do {
                Console.WriteLine("Press Enter to gracefully stop the system:");

                while (Console.KeyAvailable == false)
                {
                    ;
                }

                cki = Console.ReadKey(true);
                if (cki.Key == ConsoleKey.Enter)
                {
                    // Gracefully stop the system
                    Console.WriteLine("Entered has pressed, stopping system now");
                    foreach (IActorRef node in nodes)
                    {
                        try{
                            await node.GracefulStop(TimeSpan.FromMilliseconds(1000), new Shutdown());
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Exception occured while stopping");
                        }
                    }

                    break;
                }
            } while (true);
        }
    }
}
