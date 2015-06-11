//a ruidmentary credit tracker for an entire router
module vcr_ugal_sniffer(/*autoarg*/
   // Outputs
   credit_count,
   // Inputs
   clk, reset, flit_ctrl, flow_ctrl
   );

`include "c_functions.v"   
`include "c_constants.v"
`include "vcr_constants.v"
   // flit buffer entries per VC
   parameter num_flit_buffers 	   = 8;
   // number of message classes (e.g. request, reply)
   parameter num_message_classes   = 1;
   
   // nuber of resource classes (e.g. minimal, adaptive)
   parameter num_resource_classes  = 2;
   
   // total number of packet classes
   localparam num_packet_classes   = num_message_classes * num_resource_classes;
   
   // number of VCs per class
   parameter num_vcs_per_class 	   = 1;
   
   // number of VCs
   localparam num_vcs 		   = num_packet_classes * num_vcs_per_class;
   
   // width required to select individual VC
   localparam vc_idx_width 	   = clogb(num_vcs);
   // select packet format
   parameter packet_format = `PACKET_FORMAT_EXPLICIT_LENGTH;
   localparam flit_ctrl_width
     = (packet_format == `PACKET_FORMAT_HEAD_TAIL) ? 
       (1 + vc_idx_width + 1 + 1) : 
       (packet_format == `PACKET_FORMAT_EXPLICIT_LENGTH) ? 
       (1 + vc_idx_width + 1) : 
       -1;
   // width of flow control signals
   localparam flow_ctrl_width 	     = 1 + vc_idx_width;
   parameter reset_type = `RESET_TYPE_ASYNC;
   parameter topology 	     = `TOPOLOGY_FBFLY;
   // total number of nodes
   parameter num_nodes 		     = 64;
   
   // number of dimensions in network
   parameter num_dimensions 	     = 2;
   
   // number of nodes per router (a.k.a. concentration factor)
   parameter num_nodes_per_router    = 4;
   // total number of routers
   localparam num_routers
 				     = (num_nodes + num_nodes_per_router - 1) / num_nodes_per_router;
   // number of routers in each dimension
   localparam num_routers_per_dim    = croot(num_routers, num_dimensions);
   // number of adjacent routers in each dimension
   localparam num_neighbors_per_dim
     = (topology == `TOPOLOGY_MESH) ?
       2 :
       (topology == `TOPOLOGY_FBFLY) ?
       (num_routers_per_dim - 1) :
       -1;
   // number of input and output ports on router
   localparam num_ports
     = num_dimensions * num_neighbors_per_dim + num_nodes_per_router;
   
   localparam credit_count_width  = clogb(num_vcs*num_flit_buffers)+1;
   
   
   
   input clk;
   input reset;
   input [0:(num_ports-num_nodes_per_router)*flit_ctrl_width-1] flit_ctrl;
   //this could be improved by grabbing the flow_ctrl after the flipflop,a dn not directly from the channels
   input [0:(num_ports-num_nodes_per_router)*flow_ctrl_width-1] flow_ctrl;
   
   output [0:(num_ports-num_nodes_per_router)*credit_count_width-1] credit_count;

   generate
      genvar port;
      for(port = 0; port<num_ports-num_nodes_per_router; port=port+1)
	begin:ports
	   wire [0:credit_count_width-1] count_q;
	   wire [0:credit_count_width-1] count_s;
	   c_dff
		 #(
		   // Parameters
		   .width		(credit_count_width),
		   .reset_type		(reset_type))
	   credit_ff
	     (
	      // Outputs
		  .q			(count_q[0:credit_count_width-1]),
		  // Inputs
		  .clk			(clk),
		  .reset		(reset),
		  .d			(count_s[0:credit_count_width-1]));
	   assign count_s  = count_q
			     +flit_ctrl[port*flit_ctrl_width]
	     -flow_ctrl[port*flow_ctrl_width];
	   assign credit_count [port*credit_count_width:(port+1)*credit_count_width-1] = count_q[0:credit_count_width-1];
	   
	end
   endgenerate

   
endmodule // vcr_ugal_sniffer
