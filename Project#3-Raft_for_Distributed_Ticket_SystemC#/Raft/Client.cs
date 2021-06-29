

using System;
using System.Collections.Generic;
using System.Timers;
using Akka.Actor;
using Raft.Utilities;

namespace Raft.Client
{
    public class ClientRequest
    {
        public int command = 0;
        public IActorRef clientRef;
        public ClientRequest(int cmd, IActorRef clientRef)
        {
            this.command = cmd;
            this.clientRef = clientRef;
        }
    }
    
    public class ClientResponse
    {
        public int leaderNode = -1;
        public int[] response = new int [] {}; // index, value
        
        public ClientResponse(int node, int[] response)
        {
            this.leaderNode = node;
            this.response = response;
        }
    }

    public class ClientCommitResponse
    {
        public int leaderNode = -1;
        public int[] response = new int [] {}; // index, value

        public ClientCommitResponse(int node, int[] response)
        {
            this.leaderNode = node;
            this.response = response;
        }
    }

    public class SimClient: UntypedActor, IWithTimers
    {
        public ITimerScheduler Timers { get; set; }
        private List<IActorRef> nodes = new List<IActorRef>();
        public List<List<int>> cmd = new List<List<int>>();
        public List<List<int>> commitCmd = new List<List<int>>();
        public int lastNodeSendTo = -1;
        public int lastCmdSendIndex = -1;

        public IActorRef nearestNode;
        public int ClientId;

        public SimClient(List<IActorRef> nodes, IActorRef nNode, int ClientId)
        {
            this.nodes = nodes;
            this.nearestNode = nNode;
            this.ClientId = ClientId;
            // fill in the cmd and commitCmds
            cmd.Add(new List<int>(){0, -1});
            cmd.Add(new List<int>(){0, -2});
            cmd.Add(new List<int>(){0, -1});
            cmd.Add(new List<int>(){0, -3});
            cmd.Add(new List<int>(){0, -5});
            cmd.Add(new List<int>(){0, -6});
            cmd.Add(new List<int>(){0, -2});

            commitCmd.Add(new List<int>(){0, -1});
            commitCmd.Add(new List<int>(){0, -2});
            commitCmd.Add(new List<int>(){0, -1});
            commitCmd.Add(new List<int>(){0, -3});
            commitCmd.Add(new List<int>(){0, -5});
            commitCmd.Add(new List<int>(){0, -6});
            commitCmd.Add(new List<int>(){0, -2});
            
            for (int i = 0; i < 25; i++) 
            {
                cmd.Add(new List<int>(){0, -1});
                commitCmd.Add(new List<int>(){0, -1});
            }
            
            this.nearestNode.Tell(new ClientRequest(cmd[0][1], Self));
            lastNodeSendTo = 0;
            lastCmdSendIndex = 0;
            Timers.StartSingleTimer("client", new Timeout(), TimeSpan.FromMilliseconds(4000));
            
            // Sending Unstable from this Client
            Timers.StartPeriodicTimer("client_unstable", new UnstableReadRequest(), TimeSpan.FromMilliseconds(2000));
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Timeout tout:
                    if (lastNodeSendTo < (this.nodes.Count - 1)) lastNodeSendTo++;
                    else  lastNodeSendTo = 0;
                    
                    this.nearestNode.Tell(new ClientRequest(cmd[lastCmdSendIndex][1], Self));
                    Timers.StartSingleTimer("client", new Timeout(), TimeSpan.FromMilliseconds(4000));
                    break;
                case ClientResponse cResponse:
                    int index = cResponse.response[0];

                    if (index < this.cmd.Count)
                    {
                        this.cmd[index][0] = 1;
                        if (lastCmdSendIndex < (this.cmd.Count - 1)) lastCmdSendIndex++;
                        else
                        {
                            lastCmdSendIndex = 0;
                            Timers.Cancel("client");
                            return;
                        }
                        this.nearestNode.Tell(new ClientRequest(cmd[lastCmdSendIndex][1], Self));
                        Timers.StartSingleTimer("client", new Timeout(), TimeSpan.FromMilliseconds(4000));
                    }
                    break;
                case ClientCommitResponse ccResponse:
                    if (ccResponse.response[0] < this.commitCmd.Count) this.commitCmd[ccResponse.response[0]][0] = 1;
                    break;
                case UnstableReadRequest rr:
                    this.nearestNode.Tell(rr, Self);
                    break;
                case UnstableReadResponse rs:
                    Console.WriteLine($"Client{ClientId} unstable read response with tickets = {rs.tickets}");
                    break;
                default:
                    break;
            }
        }
    }
}