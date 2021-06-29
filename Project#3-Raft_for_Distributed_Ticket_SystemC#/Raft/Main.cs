using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Timers;
using Akka.Actor;
using Akka.Event;
using Raft.Client;
using Raft.Log;
using Raft.ReaderWriter;
using Raft.State;
using Raft.Utilities;
using Raft.Vote;


namespace Raft
{

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting Raft");
            
            var sys = ActorSystem.Create("system");
            List<IActorRef> nodes = new List<IActorRef>();
            List<IActorRef> clients = new List<IActorRef>();
            
            for (int i = 0; i < 5; i++)
            {
                nodes.Add(sys.ActorOf(Props.Create<Node>(i), $"node{i}"));
            }
            
            for (int i = 0; i < 5; i++)
            {
                clients.Add(sys.ActorOf(Props.Create<SimClient>(nodes, nodes[i], i), $"client{i}"));
            }
            
            Storage.nodes = nodes;
            Storage.clientRefs = clients;
            
            for (int i = 0; i < 5; i++)
            {
                nodes[i].Tell(new Initialize());
            }
            
            while (true)
            {
                ;
            }
        }

    }
}