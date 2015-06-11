/**
 * defines.v
 *
 * @author Kevin Woo <kwoo@cmu.edu>
 *
 * These are the parameters for the router. Please use the script
 * to generate this file as it will be more reliable
 */

// Bitwidth of the routing matrix. Should not change these unless
// there are more than 4 ports to the router not including the resource
`define rmatrix_n		4	// Number of bits in the rmatrix
`define rmatrix_w		[`rmatrix_n-1:0]
`define rmatrix_north	0	// Bit position of "north"
`define rmatrix_south	1	// Bit position of "south"
`define rmatrix_east	2	// Bit position of "east"
`define rmatrix_west	3	// Bit position of "west"
`define rmatrix_ns	1:0	// South, North field
`define rmatrix_ew	3:2	// West, East field

// Bitwidth of the assignment matrix. Should not change unless there
// are more than 6 input/output ports to the router including the resource
`define amatrix_n	5	// Number of bits in the amatrix
`define amatrix_w	[`amatrix_n-1:0]

// Bitwidth of the control line between each router. Fields are:
// [Valid][Gold][Seq][Src][Dest][Age]
`define control_n	22	// Number of bits in control field
`define control_w	[21:0]
`define valid_f	21
`define gold_f	20
`define gold_on_mask	22'd524288
`define gold_off_mask	22'd3145727
`define seq_f	19:18	// Bit position of sequence number
`define seq_w	[19:18]	// Bit position of sequence number
`define seq_n	2
`define src_f	17:14	// Bit position of source
`define src_w	[3:0]
`define dest_f	13:10
`define destx_f	13:12
`define desty_f	11:10
`define pkt_f	9:0
`define pkt_w	[9:0]
`define pkt_n	10

// These are for gold packets and flits
`define gold_w	[5:0]	// Bits for the gold register
`define l_w		[2:0]	// Bits for the l counter
`define l_limit		4	// Max value of l

// These define the address bit widths
`define addr_n	4	// Number of bits in an address
`define addrx_f	3:2	// Bit position of the X field in the address
`define addrx_w	[1:0]	// Bit width of the X field in the address
`define addry_f	1:0	// Bit position of the Y field in the address
`define addry_w	[1:0]	// Bit width of the Y field in the address
`define addrx_max	2'd3
`define addry_max	2'd3

// Width of the data portion of the router interconnect
`define data_n	128
`define data_w	[127:0]

// These define the route configuration signal between the arbitor and
// crossbar. Do not change these unless you change the number of ports
// in the router. This configuration assmes 5 I/O ports.
`define routecfg_bn	3	// Number of bits each port uses in the config
`define routecfg_n	15	// Nuber of bits in the route config
`define routecfg_w	[14:0]
`define routecfg_4	14:12	// Field for port 4 (resource)
`define routecfg_3	11:9	// Field for port 3 (west)
`define routecfg_2	8:6	// Field for port 2 (east)
`define routecfg_1	5:3	// Field for port 1 (south)
`define routecfg_0	2:0	// Field for port 0 (north)
