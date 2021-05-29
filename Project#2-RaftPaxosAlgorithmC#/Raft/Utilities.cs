

using System;
using System.Collections.Generic;
using Akka.Actor;

namespace Raft.Utilities
{
    public class Initialize { }

    public class Timeout { }

    public class Heartbeat { }

    public class Generate
    {
        public static int Between(int start, int end)
        {
            var rand = new Random();
            return rand.Next(start, end);
        }
    }

    public class Storage
    {
        public static List<IActorRef> nodes;
        public static IActorRef clientRef;
        
        public static List<IActorRef> getNodes()
        {
            return nodes;
        }

        public static IActorRef getClientRef()
        {
            return clientRef;
        }
    }
}