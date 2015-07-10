# Maze-routing

Maze-routing is a new, practical routing algorithm to tolerate
  faults in network-on-chips. The algorithm is the first to provide
  all of the following properties at the same time: 1)
  fully-distributed with no centralized component, 2) guaranteed
  delivery (it guarantees to deliver packets when a path exists between
  nodes, or otherwise indicate that destination is unreachable, while
  being deadlock and livelock free), 3) low area cost, 4) low
  reconfiguration overhead upon a fault.

For more information, *please refer to our [original paper](http://users.utu.fi/mofana/paper.php?file=NOCS%2715%20-%20Fattah,%20et%20al.pdf)* published in **NOCS'15**:
* M Fattah, A Airola, R Ausavarungnirun, N Mirzaei, P Liljeberg, J Plosila, S Mohammadi, T Pahikkala, O Mutlu and H Tenhunen,  
**A Low-Overhead, Fully-Distributed, Guaranteed-Delivery Routing Algorithm for Faulty Network-on-Chips**  
In Networks-on-Chip (NOCS), 2015 9th ACM/IEEE International Symposium on, 2015.

## The Maze-routing branch
This is a branch of NOCulator repository which implements our maze-routing algorithm.

The algorithm is implemented on top of MinBD architecture. You need to use the code in minbd folder.

### Enabling the Maze-routing algorithm
`-router.algorithm Maze_Router`


### Enabling the synthetic uniform random traffic with injection rate (inj)
`-synthGen true -synthPattern UR -synthRate (inj)`


### Fault injection settings
Starting with (min) random (link/node) failures, injecting random failures every (INT) cycles, until there are (max) failures:  
`-fault_type link/node -fault_initialCount (min) -fault_injectionInterval (INT) -fault_maxCount (max)`  
**Note! The injection interval should be a factor of 100000**
