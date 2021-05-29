Raft Consensus Algorithm paper link: https://raft.github.io/raft.pdf

--------- How to Run the program -----------

The program is implemented in the C\# The main class to run is Main.cs
under the Raft directory.

--------- Notes --------

The persistent state location is .\Raft\bin\Debug\net5.0, Under this
directory, folders with nodes IDs get made when commits are made. Make
sure to delete them after every run, otherwise logs would be different
than what Client sends in each run. This persistent storage is only used
when internal errors occurs, and nodes load their state back from this
storage. The state variables are saved in bytes as serizable for faster
access.

In each run, 6 nodes have been created with a client sending each log
enrty every 4 seconds: Client log entries = [-1,-2,1,-3,-5,6,7] = 3

With the intial state value to be 50, the eventual consistent state
after the above log = 50 + 3 = 53. The sample is included in the Raft
directory saved as "SampleRun.txt"

The current implementation does leader election, and log replication
sucessfully with the fault-tolernace for internal failures.

--------- Known Bugs -------

1)  Client doesnot have any restart mechanism so if client node fails,
    then logs remain incomplete.

2)  During the raft nodes initialization, if onReceive initialize
    message dropped before becoming follower, then that raft node remain
    un-instantiated.

3)  Raft nodes reads any logs from the persistent storage even if they
    are not from the current run of the program so, delete the
    persistent storage after every run.

4)  Client msgs can be re-ordered due to network delay as raft nodes are
    not checking the client nodes indexes. However, raft nodes would
    have same consistent logs eventually.


