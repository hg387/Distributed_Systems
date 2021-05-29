

using System;
using System.Collections.Generic;

namespace Raft.State
{
    [Serializable]
    public class Tickets
    {
        public int quantity = 0;

        public Tickets(int quantity = 50)
        {
            this.quantity = quantity;
        }

        public bool isApplicable(int q)
        {
            return (this.quantity + q >= 0);
        }

        public int applyLog(List<List<int>> log)
        {
            foreach (List<int> l in log)
            {
                int val = l[1];

                if (isApplicable(val)) this.quantity += val;
                else break;
            }

            return this.quantity;
        }
            
    }
}