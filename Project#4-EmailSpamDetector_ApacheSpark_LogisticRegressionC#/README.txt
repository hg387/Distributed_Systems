README:

The classification is done by converting the data into continuous data and extracting the features to do the classification.

The main class to run is main.py with the spark submit. 
The Benchamrks for the 2, 4, 6, and 8 clusters are attached as "Benchmark_<number of clusters>.txt"
The Benchmarks files contain the output of the program, so below is the streamlined version of the
benchmarks:

2-clusters performance:
Finished training data in 406.6331994533539 seconds
spam files classified correctly: 1179
spam files classified_incorrectly: 321
non-spam files classified correctly: 3110
non-spam files classified incorrectly: 562
Finished training data time 4.879533243179321 seconds

4-clsuters performance:
Finished training data in 382.4321620464325 seconds
spam files classified correctly: 1179
spam files classified_incorrectly: 321
non-spam files classified correctly: 3110
non-spam files classified incorrectly: 562
Finished training data time 4.815648365020752 seconds

6-clusters performance:
Finished training data in 361.1318163871765 seconds
spam files classified correctly: 1179
spam files classified_incorrectly: 321
non-spam files classified correctly: 3110
non-spam files classified incorrectly: 562
Finished training data time 4.786997318267822 seconds

8-clusters performance:
Finished training data in 350.3614761829376 seconds
spam files classified correctly: 1179
spam files classified_incorrectly: 321
non-spam files classified correctly: 3107
non-spam files classified incorrectly: 565
Finished training data time 4.464009761810303 seconds