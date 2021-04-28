using Akka.Actor;
using Akka.Util.Internal;
using LamportLock.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LamportLock.Nodes
{
    public class Node: UntypedActor, IWithTimers
    {
        public ITimerScheduler Timers { get; set; }
        private List<IActorRef> otherNodes = new List<IActorRef>();
        private Dictionary<int, int> pQueue = new Dictionary<int, int>(); 
        private int nodeId = -1;
        private Keeper clock = new Keeper();
        private int ackCount = 0;

        public Node(int nodeId)
        {
            this.nodeId = nodeId;
        }

        public void onCheck()
        {
            int nextNode = int.MaxValue;
            int min = int.MaxValue;
            foreach ((int key, int value) in pQueue)
            {
                if (value < 0) continue;

                if (value < min){
                    min = value;
                    nextNode = key;
                }
                else if (value == min && key < nextNode){
                    nextNode = key;
                }
            }

            this.clock.addOne();
            
            //Console.WriteLine($"node{this.nodeId} hits the onCheck() with ack count = {this.ackCount}");
            if (nextNode == this.nodeId && (this.ackCount == (this.otherNodes.Count-1)))
            {
                //To become Owned
                Timers.StartSingleTimer($"node{this.nodeId}", new Timeout() ,TimeSpan.FromMilliseconds(200));
                Console.WriteLine($"node{this.nodeId} now owning the resource [OWNED]");
                Become(Owned);
            }
        }

        public void onRelease(Release release)
        {
            this.pQueue[release.nodeId] = -1;
        }

        protected override void PostRestart(Exception reason)
        {
            Console.WriteLine("Node is restarting");
            Self.Forward(new Initialize());
        }

        protected void Released(object message)
        {
            switch(message)
            {
                case Timeout tout:
                    Console.WriteLine($"node{this.nodeId} broadcasting request [ACQUIRING]");

                    for (int i=0; i<this.otherNodes.Count; i++)
                    {
                        if (i != this.nodeId){
                            this.otherNodes[i].Tell(new Request(this.nodeId, this.clock.clock), Self);
                        }
                    }    
                    //this.pQueue[this.nodeId] = -1; // removes all the requests from this node
                    this.pQueue[this.nodeId] = this.clock.clock;
                    this.ackCount = 0;
                    this.clock.addOne();
                    Timers.StartSingleTimer($"node{this.nodeId}", new Timeout() ,TimeSpan.FromMilliseconds(200));
                    Become(Acquiring);
                    break;
                case Request request:
                    this.clock.setClock(Math.Max(this.clock.clock, (request.clock+1)));
                    this.pQueue[request.nodeId] = request.clock;
                    this.clock.addOne();
                    this.otherNodes[request.nodeId].Tell(new RequestResponse(this.nodeId, this.clock.clock), Self);
                    break;
                case Release release:
                    this.clock.setClock(Math.Max(this.clock.clock, (release.clock+1)));
                    this.onRelease(release);
                    this.clock.addOne();
                    break;
                case Shutdown sdown:
                    Timers.Cancel($"node{this.nodeId}");
                    Console.WriteLine($"node{this.nodeId} is stopping now");
                    Context.Stop(Self);
                    break;
                default:
                    break;
            }
        }

        protected void Owned(object message)
        {
            switch (message)
            {
                case Timeout tout:
                    // Become Released
                    Console.WriteLine($"node{this.nodeId} now releasing the resource [RELEASING]");
                    this.clock.addOne();
                    for (int i=0; i<this.otherNodes.Count; i++)
                    {
                        if (i != this.nodeId){
                            this.otherNodes[i].Tell(new Release(this.nodeId, this.clock.clock), Self);
                        }
                    }
                    Timers.StartSingleTimer($"node{this.nodeId}", new Timeout() ,TimeSpan.FromMilliseconds(200));
                    Become(Released);
                    break;
                case Request request:
                    this.clock.setClock(Math.Max(this.clock.clock, (request.clock+1)));
                    this.pQueue[request.nodeId] = request.clock;
                    this.clock.addOne();
                    this.otherNodes[request.nodeId].Tell(new RequestResponse(this.nodeId, this.clock.clock), Self);
                    break;
                case Shutdown sdown:
                    Timers.Cancel($"node{this.nodeId}");
                    Console.WriteLine($"node{this.nodeId} now releasing the resource [RELEASING]");
                    Console.WriteLine($"node{this.nodeId} is stopping now");
                    Context.Stop(Self);
                    break;
                default:
                    break;
            }
        }

        protected void Acquiring(object message)
        {
            switch (message)
            {
                case Timeout tout:
                    Timers.StartSingleTimer($"node{this.nodeId}", new Timeout() ,TimeSpan.FromMilliseconds(200));
                    this.onCheck();
                    break;
                case Request request:
                    this.clock.setClock(Math.Max(this.clock.clock, (request.clock+1)));
                    this.pQueue[request.nodeId] = request.clock;
                    this.clock.addOne();
                    this.otherNodes[request.nodeId].Tell(new RequestResponse(this.nodeId, this.clock.clock), Self);
                    break;
                case RequestResponse rr:
                    this.clock.setClock(Math.Max(this.clock.clock, (rr.clock+1)));
                    this.ackCount += 1;
                    this.clock.addOne();
                    break;
                case Release release:
                    this.clock.setClock(Math.Max(this.clock.clock, (release.clock+1)));
                    this.onRelease(release);
                    this.clock.addOne();
                    break;
                case Shutdown sdown:
                    Timers.Cancel($"node{this.nodeId}");
                    Console.WriteLine($"node{this.nodeId} is stopping now");
                    Context.Stop(Self);
                    break;
                default:
                    break;
            }
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Initialize init:
                    this.otherNodes = Initialize.otherNodes;
                    this.pQueue = Initialize.pQueue;
                    Console.WriteLine($"node{this.nodeId} is started");
                    Console.WriteLine($"node{this.nodeId} broadcasting request [ACQUIRING]");

                    for (int i=0; i<this.otherNodes.Count; i++)
                    {
                        if (i != this.nodeId){
                            this.otherNodes[i].Tell(new Request(this.nodeId, this.clock.clock), Self);
                        }
                    }    
                    this.clock.addOne();
                    this.pQueue[this.nodeId] = this.clock.clock;
                    Timers.StartSingleTimer($"node{this.nodeId}", new Timeout() ,TimeSpan.FromMilliseconds(200));
                    Become(Acquiring);
                    break;
                default:
                    Self.Forward(message);
                    break;
            }
        }
    }
}