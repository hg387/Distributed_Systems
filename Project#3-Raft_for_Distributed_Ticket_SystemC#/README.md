--------- How to Run the program -----------

The program is implemented in the C\# The main class to run is Main.cs
under the Raft directory.

--------- Notes --------

The persistent state location is \Raft\bin\Debug\net5.0, Under this
directory, folders with nodes IDs get made when commits are made. Make
sure to delete them after every run, otherwise logs would be different
than what Client sends in each run. This persistent storage is only used
when internal errors occurs, and nodes load their state back from this
storage. The state variables are saved in bytes as serizable for faster
access.

In each run, 6 nodes have been created with a client sending each log
enrty every 4 seconds:

With the intial state value to be 50, the eventual consistent state
value with the mentioned workload config in the reflection is Number of
tickets = 5.

The sample is included in the Raft directory saved as "SampleRun.txt"
