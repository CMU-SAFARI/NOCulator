

/*
 
 ONLY SUPPORT 2 REsOURCE clASSES, ZOMG ZOMG ZOMG ZOMG  ZZOMG
 
 */
module vcr_ugal_source(/*autoarg*/
   // Outputs
   intm_router_address, route_min,
   // Inputs
   clk, reset, flit_valid, flit_head, src_router_address, route_info,
   credit_count
   );
   
`include "c_functions.v"
`include "c_constants.v"

   parameter ugal_threshold 	     = 3;
   
   // flit bufwfer entries per VC
   parameter num_flit_buffers 	     = 8;
   // number of message classes (e.g. request, reply)
   parameter num_message_classes     = 1;
   
   // nuber of resource classes (e.g. minimal, adaptive)
   parameter num_resource_classes    = 2;
   
   // total number of packet classes
   localparam num_packet_classes     = num_message_classes * num_resource_classes;
   
   // number of VCs per class
   parameter num_vcs_per_class 	     = 1;
   
   // number of VCs
   localparam num_vcs 		     = num_packet_classes * num_vcs_per_class;
   //
   parameter port_id 		     = 6;
   
   // number of routers in each dimension
   parameter num_routers_per_dim     = 4;
   
   // width required to select individual router in a dimension
   localparam dim_addr_width 	     = clogb(num_routers_per_dim);
   
   // number of dimensions in network
   parameter num_dimensions 	     = 2;
   
   // width required to select individual router in network
   localparam router_addr_width      = num_dimensions * dim_addr_width;
   
   // number of nodes per router (a.k.a. consentration factor)
   parameter num_nodes_per_router    = 4;
   
   // width required to select individual node at current router
   localparam node_addr_width 	     = clogb(num_nodes_per_router);
   
   // width of global addresses
   localparam addr_width 	     = router_addr_width + node_addr_width;
   
   // connectivity within each dimension
   parameter connectivity 	     = `CONNECTIVITY_FULL;
   
   // number of adjacent routers in each dimension
   localparam num_neighbors_per_dim
     = ((connectivity == `CONNECTIVITY_LINE) ||
	(connectivity == `CONNECTIVITY_RING)) ?
       2 :
       (connectivity == `CONNECTIVITY_FULL) ?
       (num_routers_per_dim - 1) :
       -1;
   
   // number of input and output ports on router
   localparam num_ports
     = num_dimensions * num_neighbors_per_dim + num_nodes_per_router;
   
   // width required to select an individual port
   localparam port_idx_width 	   = clogb(num_ports);
   
   // select routing function type
   parameter routing_type 	   = `ROUTING_TYPE_DOR;
   
   // select order of dimension traversal
   parameter dim_order 		   = `DIM_ORDER_ASCENDING;
   
   // total number of bits required for storing routing information
   localparam route_info_width
     = num_resource_classes * router_addr_width + node_addr_width;
   
   parameter reset_type 	   = `RESET_TYPE_ASYNC;


   
   input clk;
   input reset;
   input flit_valid;
   input flit_head;
 
	 
   // current router's address
   input [0:router_addr_width-1] src_router_address;
   //destination address
   input [0:addr_width-1] route_info;
   
   localparam credit_count_width  = clogb(num_vcs*num_flit_buffers)+1;
   //credit count from the ugal_sniffer
   input [0:(num_ports-num_nodes_per_router)*credit_count_width-1] credit_count;
   
   // modified routing data
   output [0:router_addr_width-1] intm_router_address;
   // modified resource class
   output route_min;

   //normal routing on the min path
   localparam [0:num_resource_classes-1] sel_irc
     = (1 << (num_resource_classes - 1-1));
   wire [0:num_ports-1] min_route_unmasked;
   wire 			       inc_rc;
    vcr_routing_logic
      #(.num_resource_classes(num_resource_classes),
	.num_routers_per_dim(num_routers_per_dim),
	.num_dimensions(num_dimensions),
	.num_nodes_per_router(num_nodes_per_router),
	.connectivity(connectivity),
	.routing_type(routing_type),
	.dim_order(dim_order),
	.reset_type(reset_type))
    min_routing
      (.clk(clk),
       .reset(reset),
       .router_address(src_router_address),
       .route_info({src_router_address,route_info}),
       .route_op(min_route_unmasked),
       .inc_rc(inc_rc),
       .sel_rc(sel_irc));

   //normal routing on nonmin route
   localparam [0:num_resource_classes-1] sel_irc_nonmin
     = (1 << (num_resource_classes - 1-0));

   wire [0:num_ports-1] nonmin_route_unmasked;
   wire 			       inc_rc_nonmin;
   vcr_routing_logic_ugal
      #(.num_resource_classes(num_resource_classes),
	.num_routers_per_dim(num_routers_per_dim),
	.num_dimensions(num_dimensions),
	.num_nodes_per_router(num_nodes_per_router),
	.connectivity(connectivity),
	.routing_type(routing_type),
	.dim_order(dim_order),
	.reset_type(reset_type))
    nonmin_routing
      (.clk(clk),
       .reset(reset),
       .router_address(src_router_address),
       .route_info({intm_router_address,route_info}),
       .route_op(nonmin_route_unmasked),
       .inc_rc(inc_rc_nonmin),
       .sel_rc(sel_irc_nonmin));


   wire [0:credit_count_width-1] min_count;
   wire [0:credit_count_width-1]nonmin_count;

   //select the credit count for min/nonmin ports
   c_select_1ofn
     #(// Parameters
       .num_ports			(num_ports-num_nodes_per_router),
       .width				(credit_count_width))
   min_count_select
     (
      // Outputs
      .data_out				(min_count[0:credit_count_width-1]),
      // Inputs
      .select				(min_route_unmasked[0:num_ports-num_nodes_per_router-1]),
      .data_in				( credit_count[0:(num_ports-num_nodes_per_router)*credit_count_width-1]));
   c_select_1ofn
     #(// Parameters
       .num_ports			(num_ports-num_nodes_per_router),
       .width				(credit_count_width))
   nonmin_count_select
     (
      // Outputs
      .data_out				(nonmin_count[0:credit_count_width-1]),
      // Inputs
      .select				(nonmin_route_unmasked[0:num_ports-num_nodes_per_router-1]),
      .data_in				( credit_count[0:(num_ports-num_nodes_per_router)*credit_count_width-1]));


   wire compare ;
   wire compare_q;

   //shift the nonmin count by 1 (X2) and compare
   
   wire [0:credit_count_width-1+2] ext_min_count;
   assign  ext_min_count [0:credit_count_width-1+2] = {min_count[0:credit_count_width-1],2'b0};
   wire [0:credit_count_width-1+2] ext_nonmin_count;
   assign  ext_nonmin_count [0:credit_count_width-1+2] = {1'b0,nonmin_count[0:credit_count_width-1],1'b0};
   
   assign compare 	=( (ext_nonmin_count+ugal_threshold) > ext_min_count);

   //keep resource classes of multiflit packet consistent 
   wire decision 	= (flit_head&flit_valid)?compare:compare_q;

   localparam eligible 	= num_ports-num_nodes_per_router;

   //select the resource class
   assign route_min 	= |(min_route_unmasked[eligible:num_ports-1])|decision;
   //assign route_min 	= 1'b1;
   
   //remember the routing decision for multiflit packets
   c_dff
     #(       // Parameters
       .width				(1),
       .reset_type			(reset_type),
       .reset_value			(1'b1))
   last_compare
     (
      // Outputs
      .q				(compare_q),
      // Inputs
      .clk				(clk),
      .reset				(reset),
      .d				((flit_head&flit_valid)?compare:compare_q));
   

   //LFSR that generate a random router address
   //note only 1 feedback value is used, could be really 'not" random
   
   wire [0:router_addr_width-1] rand_value;
   wire [0:router_addr_width-1] rand_feedback;
   
   c_fbgen
     #(.width(router_addr_width),
       .index(1))
   rand_fbgen
     (.feedback(rand_feedback));
   c_lfsr
     #(
       // Parameters
       .width			( router_addr_width),
       .iterations	       	( router_addr_width),
       .reset_type		(reset_type))
   rand_gen
     (
      // Outputs
      .q			(rand_value[0:router_addr_width-1]),
      // Inputs
      .clk			(clk),
      .reset			(reset),
      .load			(reset),
      .run			(flit_head&flit_valid),
      .feedback			(rand_feedback[0:router_addr_width-1]),
      .complete			(1'b0),
      .d			(rand_value[0:router_addr_width-1]));

   wire carry;
   //select a random intermediate router if necessary.
   assign {intm_router_address[0:router_addr_width-1],carry} = {src_router_address[0:router_addr_width-1],1'b0}+{rand_value[0:router_addr_width-1],1'b0}; 
   //assign intm_router_address[0:router_addr_width-1] = 6'b0;
   
endmodule

