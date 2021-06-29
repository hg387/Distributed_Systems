

using System;
using System.Collections.Generic;
using Akka.Actor;

namespace Raft.Utilities
{
    public class Initialize { }
    
    public class UnstableReadRequest { }

    public class UnstableReadResponse
    {
        public int tickets;

        public UnstableReadResponse(int tickets)
        {
            this.tickets = tickets;
        }
    }

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
        public static List<IActorRef> clientRefs;

        public static List<IActorRef> getNodes()
        {
            return nodes;
        }

        public static List<IActorRef> getClientRef()
        {
            return clientRefs;
        }

        public static IActorRef getClientRef(int i)
        {
            if (i < clientRefs.Count) return clientRefs[i];
            return null;
        }
    }
}