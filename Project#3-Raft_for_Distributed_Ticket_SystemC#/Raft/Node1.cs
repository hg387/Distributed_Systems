using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Timers;
using Akka.Actor;
using Akka.Event;
using Akka.Util.Internal;
using Raft.Client;
using Raft.Log;
using Raft.ReaderWriter;
using Raft.State;
using Raft.Utilities;
using Raft.Vote;

namespace Raft
{
    public class Node: UntypedActor, IWithTimers
    {
        public ITimerScheduler Timers { get; set; }
        private IActorRef clientRef = null;
        
        // Stable State
        private int currentTerm = 0;
        private int votedFor = -1;
        private List<List<int>> log = new List<List<int>>();
        private int commitLength = 0;
        private Tickets tickets = new Tickets();

        // Transient State
        private int nodeId; // Props
        private List<IActorRef> otherNodes = new List<IActorRef>(); // Props
        private string currentRole = "follower";
        private int currentLeader = -1;
        private HashSet<int> votesReceived = new HashSet<int>();
        private Dictionary<int, int> sentLength = new Dictionary<int, int>();
        private Dictionary<int, int> ackedLength = new Dictionary<int, int>();

        public Node(int nodeId)
        {
            this.nodeId = nodeId;
        }

        protected override void PreStart()
        {
            if (ReadWriteState.IfExists($"node{nodeId}", "currentTerm") &&
                ReadWriteState.IfExists($"node{nodeId}", "votedFor") &&
                ReadWriteState.IfExists($"node{nodeId}", "log") &&
                ReadWriteState.IfExists($"node{nodeId}", "commitLength")
            )
            {
                this.currentTerm = (int) ReadWriteState.Read($"node{nodeId}", "currentTerm");
                this.votedFor = (int) ReadWriteState.Read($"node{nodeId}", "votedFor");
                this.log = (List<List<int>>) ReadWriteState.Read($"node{nodeId}", "log");
                this.commitLength = (int) ReadWriteState.Read($"node{nodeId}", "commitLength");
                this.tickets = (Tickets) ReadWriteState.Read($"node{nodeId}", "tickets");
            }
        }

        protected override void PostRestart(Exception cause)
        {
            Console.WriteLine("node restarted");

            Self.Forward(new Initialize());
        }

        public void WriteStableState()
        {
            ReadWriteState.Write($"node{nodeId}", "currentTerm", this.currentTerm);
            ReadWriteState.Write($"node{nodeId}", "votedFor", this.votedFor);
            ReadWriteState.Write($"node{nodeId}", "log", this.log); 
            ReadWriteState.Write($"node{nodeId}", "commitLength", this.commitLength);
            ReadWriteState.Write($"node{nodeId}", "tickets", this.tickets);
        }

        public void OnTimeOut()
        {
            this.currentTerm += 1;
            this.currentRole = "candidate";
            this.votedFor = this.nodeId;
            this.votesReceived.Add(nodeId);

            int lastTerm = 0;

            if (this.log.Count > 0)
            {
                lastTerm = this.log[^1][0];
            }

            VoteRequest msg = new VoteRequest(this.nodeId, this.currentTerm, this.log.Count, lastTerm);

            for (int i=0;i<this.otherNodes.Count;i++)
            {
                if (i != this.nodeId)
                {
                    IActorRef node = this.otherNodes[i];
                    node.Tell(msg, Self);
                }
            }
            Timers.StartSingleTimer($"node{nodeId}_election", new Timeout(), TimeSpan.FromSeconds(Generate.Between(150,301)));
            Console.WriteLine($"node{nodeId} became candidate");
            Become(Candidate);
        }

        public bool OnVoteRequest(VoteRequest request)
        {
            int myLogTerm = 0;
            if (this.log.Count > 0) myLogTerm = this.log[^1][0];
            
            bool logOk = (request.cLogTerm > myLogTerm) || ((request.cLogTerm == myLogTerm) &&
                                                            (request.cLogLength >= this.log.Count));

            bool termOk = (request.cTerm > this.currentTerm) || ((request.cTerm == this.currentTerm) &&
                                                                 (this.votedFor == -1 || this.votedFor == request.cId));

            if (logOk && termOk)
            {
                this.currentTerm = request.cTerm;
                this.currentRole = "follower";
                this.votedFor = request.cId;
                this.otherNodes[request.cId].Tell(new VoteResponse(this.nodeId, this.currentTerm, true), Self);
                Console.WriteLine($"node{nodeId} accepted vote of node{request.cId}");
                return true;
            }
            else
            {
                this.otherNodes[request.cId].Tell(new VoteResponse(this.nodeId, this.currentTerm, false), Self);
                Console.WriteLine($"node{nodeId} rejected vote of node{request.cId}");
                return false;
            }
        }

        public void ReplicateLog(int leaderId, int followerId)
        {
            int i = this.sentLength[followerId];

            List<List<int>> entries = new List<List<int>>();
            if (i < this.log.Count) entries = new List<List<int>>(this.log.GetRange(i, (this.log.Count - i)));
            int prevLogTerm = 0;

            if (i > 0) prevLogTerm = this.log[i - 1][0];
            
            this.otherNodes[followerId].Tell(new LogRequest(leaderId, this.currentTerm, i, 
                prevLogTerm, this.commitLength, entries),Self);
            
            return;
        }

        public void onLogRequest(LogRequest request)
        {
            if (request.term > this.currentTerm)
            {
                this.currentTerm = request.term;
                this.votedFor = -1;
                this.currentRole = "follower";
                this.currentLeader = request.leaderId;
                
                // Cancels all timers
                Timers.Cancel($"node{nodeId}_election");
                Timers.Cancel($"node{nodeId}_heartbeat");
            }

            if (request.term == this.currentTerm && this.currentRole.Equals("candidate"))
            {
                this.currentRole = "follower";
                this.currentLeader = request.leaderId;
                
                // Cancels all timers
                Timers.Cancel($"node{nodeId}_election");
                Timers.Cancel($"node{nodeId}_heartbeat");
            }
            
            bool logOk = (this.log.Count >= request.logLength) &&
                         (request.logLength == 0 || request.logTerm == this.log[request.logLength-1][0]);

            if (request.term == this.currentTerm && logOk)
            {
                AppendEntries(request.logLength, request.leaderCommit, request.entries);
                int ack = request.logLength + request.entries.Count;
                this.otherNodes[request.leaderId].Tell(new LogResponse(this.nodeId, this.currentTerm, ack, true));
            }
            else
            {
                this.otherNodes[request.leaderId].Tell(new LogResponse(this.nodeId, this.currentTerm, 0, false));
            }

            if (request.term >= this.currentTerm && this.currentRole.Equals("follower"))
            {
                Timers.StartSingleTimer($"node{nodeId}_election", new Timeout(), TimeSpan.FromSeconds(Generate.Between(150,301)));
                Become(Follower);
            }
        }

        public void AppendEntries(int logLength, int leaderCommit, List<List<int>> entries)
        {
            if (entries.Count > 0 && this.log.Count > logLength)
            {
                if (this.log[logLength][0] != entries[0][0])
                {
                    this.log = new List<List<int>>(this.log.GetRange(0, logLength));
                }            
            }

            if (logLength + entries.Count > this.log.Count)
            {
                for (int i = (this.log.Count - logLength); i <= (entries.Count - 1); i++)
                {
                    this.log.Add(entries[i]);
                }
            }

            if (leaderCommit > this.commitLength)
            {
                
                WriteStableState();
                
                Console.WriteLine($"node{nodeId} total logs are");
                this.log.ForEach(l => Console.WriteLine(String.Join(" ", l)));
                Console.WriteLine($"node{nodeId} committed new logs are");
                this.log.GetRange((this.commitLength), (leaderCommit - this.commitLength)).ForEach(kl => 
                    Console.WriteLine(String.Join(" ", kl)));
                
                // Apply the logs
                //this.tickets.applyLog(this.log.GetRange((this.commitLength-1), (leaderCommit-this.commitLength+1)));
                try
                {
                    this.tickets.applyLog(this.log.GetRange((this.commitLength), (leaderCommit - this.commitLength)));
                }
                catch(Exception e)
                {
                    Console.WriteLine("Exception occured in indexing and applying logs");
                }
                Console.WriteLine($"node{nodeId} current state is {this.tickets.quantity}");
                for (int i = this.commitLength; i <= (leaderCommit - 1); i++)
                {
                    //Console.WriteLine($"node{nodeId} applying logs");
                }
                this.commitLength = leaderCommit;
            }
        }

        public void CommitLogEntries()
        {
            HashSet<int> ready = new HashSet<int>();
            int minAcks = (this.otherNodes.Count + 1) / 2;
            for (int len = 1; len <= this.log.Count; len++)
            {
                int count = 0;
                for (int n = 0; n < this.otherNodes.Count; n++)
                {
                    if (this.ackedLength.ContainsKey(n))
                    {
                        if (this.ackedLength[n] >= len) count++;
                    }
                }

                if (count >= minAcks) ready.Add(len);
            }

            if (ready.Count != 0 && ready.Max() > this.commitLength &&
                this.log[ready.Max() - 1][0] == this.currentTerm)
            {
                //Apply the logs
                WriteStableState();
                //this.tickets.applyLog(this.log.GetRange((this.commitLength-1), (ready.Max()-this.commitLength+1)));
                
                Console.WriteLine($"node{nodeId} total logs are");
                this.log.ForEach(l => Console.WriteLine(String.Join(" ", l)));
                Console.WriteLine($"node{nodeId} committed new logs are");
                this.log.GetRange((this.commitLength), (ready.Max() - this.commitLength)).ForEach(kl => 
                    Console.WriteLine(String.Join(" ", kl)));

                try
                {
                    this.tickets.applyLog(this.log.GetRange((this.commitLength), (ready.Max()-this.commitLength)));
                
                    Console.WriteLine($"node{nodeId} current state is {this.tickets.quantity}");
                    for (int i = (this.commitLength); i <= (ready.Max() - 1); i++)
                    {
                        //this.tickets.applyLog(this.log);
                        //Console.WriteLine($"Leader node{nodeId} applying logs");
                        
                        // Reply commit back to the Client
                        clientRef.Tell(new ClientCommitResponse(this.nodeId, new int[]{(i), this.log[i][1]}));
                    }
                    this.commitLength = ready.Max();
                }
                catch(Exception e)
                {
                    Console.WriteLine("Exception occured in indexing and applying logs");
                }
            }
        }

        protected void Follower(object message)
        {
            switch (message)
            {
                case Timeout tout:
                    Console.WriteLine($"node{nodeId} follower timedOut");
                    OnTimeOut();
                    break;
                case VoteRequest request:
                    bool tmp = OnVoteRequest(request);
                    if (tmp)
                    {
                        Timers.StartSingleTimer($"node{nodeId}_election", new Timeout(), TimeSpan.FromSeconds(Generate.Between(150,301)));
                        Console.WriteLine($"node{nodeId} became follower");
                        Become(Follower);
                    }
                    break;
                case ClientRequest cRequest:
                    if (this.currentLeader != -1) this.otherNodes[this.currentLeader].Tell(cRequest,Self);
                    break;
                case LogRequest lRequest:
                    onLogRequest(lRequest);
                    break;
                case UnstableReadRequest rr:
                    if (this.log.Count > 0)
                    {
                        Tickets tmpT = new Tickets();
                        int toSendT = tmpT.applyLog(new List<List<int>>(this.log));
                        this.clientRef.Tell(new UnstableReadResponse(toSendT), Self);
                    }
                    break;
                default:
                    break;
            }
        }

        protected void Candidate(object message)
        {
            switch (message)
            {
                case Timeout tout:
                    Console.WriteLine($"node{nodeId} follower timedOut");
                    OnTimeOut();
                    break;
                case VoteRequest request:
                    bool tmp = OnVoteRequest(request);
                    if (tmp)
                    {
                        Timers.StartSingleTimer($"node{nodeId}_election", new Timeout(), TimeSpan.FromSeconds(Generate.Between(150,301)));
                        Console.WriteLine($"node{nodeId} became follower");
                        Become(Follower);
                    }
                    break;
                case VoteResponse response:
                    if (this.currentRole.Equals("candidate") && this.currentTerm == response.term && response.granted)
                    {
                        this.votesReceived.Add(response.voterId);

                        if (this.votesReceived.Count >= ((this.otherNodes.Count + 1) / 2))
                        {
                            this.currentRole = "leader";
                            this.currentLeader = this.nodeId;
                            Timers.Cancel($"node{nodeId}_election");
                            
                            for (int i = 0; i < this.otherNodes.Count; i++)
                            {
                                if (i != this.nodeId)
                                {
                                    this.sentLength[i] = this.log.Count;
                                    this.ackedLength[i] = 0;
                                    ReplicateLog(this.nodeId, i);
                                }
                            }
                            // start a heartbeat when become a leader
                            Timers.StartPeriodicTimer($"node{nodeId}_heartbeat", new Heartbeat(), TimeSpan.FromSeconds(Generate.Between(100,151)));
                            Console.WriteLine($"node{nodeId} became leader");
                            Become(Leader);
                        }
                    }
                    else if (response.term > this.currentTerm)
                    {
                        this.currentTerm = response.term;
                        this.currentRole = "follower";
                        this.votedFor = -1;
                        Timers.StartSingleTimer($"node{nodeId}_election", new Timeout(), TimeSpan.FromSeconds(Generate.Between(150,301)));
                        Console.WriteLine($"node{nodeId} became follower");
                        Become(Follower);    
                    }

                    break;
                case ClientRequest cRequest:
                    if (this.currentLeader != -1) this.otherNodes[this.currentLeader].Tell(cRequest,Self);
                    break;
                case LogRequest lRequest:
                    onLogRequest(lRequest);
                    break;
                case UnstableReadRequest rr:
                    if (this.log.Count > 0)
                    {
                        Tickets tmpT = new Tickets();
                        int toSendT = tmpT.applyLog(new List<List<int>>(this.log));
                        this.clientRef.Tell(new UnstableReadResponse(toSendT), Self);
                    }
                    break;
                default:
                    break;
                    
            }
        }
        
        protected void Leader(object message)
        {
            switch (message)
            {
                case VoteRequest request:
                    bool tmp = OnVoteRequest(request);
                    if (tmp)
                    {
                        Timers.StartSingleTimer($"node{nodeId}_election", new Timeout(), TimeSpan.FromSeconds(Generate.Between(150,301)));
                        Timers.Cancel($"node{nodeId}_heartbeat");
                        Console.WriteLine($"node{nodeId} became follower");
                        Become(Follower);
                    }
                    break;
                case ClientRequest cRequest:
                    if (this.currentRole.Equals("leader"))
                    {
                        Console.WriteLine($"leader node{nodeId} received client request {cRequest.command}");
                        this.log.Add(new List<int>(){this.currentTerm, cRequest.command});
                        this.ackedLength[this.nodeId] = this.log.Count;
                        
                        for (int i = 0; i < this.otherNodes.Count; i++)
                        {
                            if (i != this.nodeId)
                            {
                                ReplicateLog(this.nodeId, i);
                            }
                        }
                        
                        // Reply back to the Client (Checked)
                        cRequest.clientRef.Tell(new ClientResponse(this.nodeId, new int[]{(this.log.Count-1), this.log[^1][1]}));
                    }
                    break;
                case Heartbeat hbeat:
                    if (this.currentRole.Equals("leader"))
                    {
                        for (int i = 0; i < this.otherNodes.Count; i++)
                        {
                            if (i != this.nodeId)
                            {
                                ReplicateLog(this.nodeId, i);
                            }
                        }
                    }
                    break;
                /*case LogRequest lRequest:
                    onLogRequest(lRequest);
                    break;
                */
                case LogResponse lResponse:
                    if (lResponse.term == this.currentTerm && this.currentRole.Equals("leader"))
                    {
                        if (lResponse.success && lResponse.ack >= this.ackedLength[lResponse.follower])
                        {
                            this.sentLength[lResponse.follower] = lResponse.ack;
                            this.ackedLength[lResponse.follower] = lResponse.ack;
                            CommitLogEntries();
                        }
                        else if (this.sentLength[lResponse.follower] > 0)
                        {
                            this.sentLength[lResponse.follower] -= 1;
                            ReplicateLog(this.nodeId, lResponse.follower);
                        }
                    }
                    else if (lResponse.term > this.currentTerm)
                    {
                        this.currentTerm = lResponse.term;
                        this.currentRole = "follower";
                        this.votedFor = -1;
                        Timers.Cancel($"node{nodeId}_heartbeat");
                        Timers.StartSingleTimer($"node{nodeId}_election", new Timeout(), TimeSpan.FromSeconds(Generate.Between(150,301)));
                        Become(Follower);
                    }
                    break;
                case UnstableReadRequest rr:
                    if (this.log.Count > 0)
                    {
                        Tickets tmpT = new Tickets();
                        int toSendT = tmpT.applyLog(new List<List<int>>(this.log));
                        this.clientRef.Tell(new UnstableReadResponse(toSendT), Self);
                    }

                    break;
                default:
                    break;
            }
        }

        protected override void OnReceive(object message) {
            Console.WriteLine($"node{nodeId} hits OnReceive");
            switch (message)
            {
                case Initialize initialize:
                    this.otherNodes = Storage.nodes;
                    this.clientRef = Storage.getClientRef(this.nodeId);
                    break;
                default:
                    Self.Forward(message);
                    break;
            }
            
            if (this.otherNodes.Count > 0 && this.clientRef != null)
            {
                Console.WriteLine($"node{nodeId} is started");
                this.currentRole = "follower";
                Timers.StartSingleTimer($"node{nodeId}_election", new Timeout(), TimeSpan.FromSeconds(Generate.Between(150,301)));
                Console.WriteLine($"node{nodeId} became follower");
                Become(Follower);
            }
            return;
        }

    }
}