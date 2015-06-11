/*
  RingClustered Network. 
*/
// Bitwidth of the routing matrix. Should not change unless there are more
//  than 4 input/output ports to the router not including the resource.
`define rmatrix_n       4   // Number of bits in the rmatrix
`define rmatrix_w       [`rmatrix_n-1:0]
`define rmatrix_north   0   // Bit position of "north"
`define rmatrix_south   1   // Bit position of "south"
`define rmatrix_east    2   // Bit position of "east"
`define rmatrix_west    3   // Bit position of "west"
`define rmatrix_ns      1:0 // South, North field
`define rmatrix_ew      3:2 // West, East field

// Bitwidth of the assignment matrix. Should not change unless there are
//  more than 6 input/output ports to the router including the resource
`define amatrix_n   5       // Number of bits in the amatrix
`define amatrix_w   [`amatrix_n-1:0]

// Bitwidth of the control line between each router. The layout is as follows:
// [MSHR(4)][Seq(3)][Valid][Source(4)][Destination(4)]
`define control_n   16+128      // Number of bits in the control field: INCLUDE DATA
`define control_w   [`control_n-1:0]
`define mshr_f      15:12
`define valid_f     11      // Bit position of valid, at most 1 bit
`define seq_f       10:8   // Bit postion of sequence
`define seq_n       3
`define src_f       7:4    // Bit position of source
`define src_w       [3:0]
`define dest_f      3:0     // Bit position of destination
`define destx_f     3:2     // Bit position of the X field in dest
`define desty_f     1:0     // Bit position of the Y field in dest

// steer field tacked on to left within arbiter network
`define steer_n (`control_n+0)
`define steer_w [`steer_n-1:0]

// These define the address bit widths
`define addr_n      4      // Number of bits in an address
`define addrx_f     3:2     // Bit position of the X field in address
`define addrx_w     [1:0]   // Bit width of the X field in address
`define addry_f     1:0     // Bit position of the Y field in the address
`define addry_w     [1:0]   // Bit width of the Y field in the address
`define addrx_max   2'b11   // The maximum X address
`define addry_max   2'b11   // The maximum Y address

// Width of the data portion of the connection between the routers
`define data_n  128
`define data_w  [`data_n-1:0] 

// These define the route configuration signal between the arbitor and crossbar.
// Do not change these unless you change the number of ports in the router
// This configuration assuming 5 I/O ports.
`define routecfg_bn 3       // Number of bits each port uses in the config
`define routecfg_n  15      // Number of bits in the route config
`define routecfg_w  [`routecfg_n-1:0]
`define routecfg_4  14:12   // Field for port 4 (Resource)
`define routecfg_3  11:9    // Field for port 3 (West)
`define routecfg_2  8:6     // Field for port 2 (East)
`define routecfg_1  5:3     // Field for port 1 (South)
`define routecfg_0  2:0     // Field for port 0 (North)

