

namespace Raft.Vote
{
    public class VoteResponse
    {
        public int voterId;
        public int term;
        public bool granted;

        public VoteResponse(int voterId, int term, bool granted)
        {
            this.voterId = voterId;
            this.term = term;
            this.granted = granted;
        }
    }

    public class VoteRequest
    {
        public int cId;
        public int cTerm;
        public int cLogLength;
        public int cLogTerm;

        public VoteRequest(int cId, int cTerm, int cLogLength, int cLogTerm)
        {
            this.cId = cId;
            this.cTerm = cTerm;
            this.cLogLength = cLogLength;
            this.cLogTerm = cLogTerm;
        }
            
        
    }
}