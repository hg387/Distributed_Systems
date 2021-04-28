using System.Collections.Generic;
using System.Globalization;
using Akka.Actor;
using Akka.Util.Internal;

namespace LamportLock.Utilities
{
    public class Shutdown {}
    public class Timeout {}

    public class Keeper
    {
        public int clock = 0;

        public void setClock(int clock)
        {
            this.clock = clock;
        }

        public void addOne()
        {
            this.clock += 1;
        }

        public void reset()
        {
            this.clock = 0;
        }

    }

    public class Release
    {
        public int nodeId;
        public int clock;

        public Release(int nodeId, int clock)
        {
            this.nodeId = nodeId;
            this.clock = clock;
        }
    }

    public class Request
    {
        public int nodeId;
        public int clock;

        public Request(int nodeId, int clock)
        {
            this.nodeId = nodeId;
            this.clock = clock;
        }
    }

    public class RequestResponse
    {
        public int nodeId;
        public int clock;

        public RequestResponse(int nodeId, int clock)
        {
            this.nodeId = nodeId;
            this.clock = clock;
        }
    }

    public class Initialize {
        public static List<IActorRef> otherNodes = new List<IActorRef>();
        public static Dictionary<int, int> pQueue = new Dictionary<int, int>();

        public static void setPQueue()
        {
            for (int i=0; i<otherNodes.Count; i++)
            {
                pQueue.Add(i, -1);
            }
        }
    }
}