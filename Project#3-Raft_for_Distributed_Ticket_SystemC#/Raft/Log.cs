using System.Collections.Generic;

namespace Raft.Log
{
    public class LogRequest
    {
        public int leaderId;
        public int term;
        public int logLength;
        public int logTerm;
        public int leaderCommit;
        public List<List<int>> entries = new List<List<int>>();

        public LogRequest(int leaderId, int term, int logLength, int logTerm, int leaderCommit, List<List<int>> entries){
            this.leaderId = leaderId;
            this.term = term;
            this.logLength = logLength;
            this.logTerm = logTerm;
            this.leaderCommit = leaderCommit;
            this.entries = entries;
        }
            
    }

    public class LogResponse
    {
        public int follower;
        public int term;
        public int ack;
        public bool success;

        public LogResponse(int follower, int term, int ack, bool success)
        {
            this.follower = follower;
            this.term = term;
            this.ack = ack;
            this.success = success;
        }
    }
}