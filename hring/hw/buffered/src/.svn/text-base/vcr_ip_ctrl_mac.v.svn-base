// $Id$

/*
Copyright (c) 2007-2009, Trustees of The Leland Stanford Junior University
All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

Redistributions of source code must retain the above copyright notice, this list
of conditions and the following disclaimer.
Redistributions in binary form must reproduce the above copyright notice, this 
list of conditions and the following disclaimer in the documentation and/or 
other materials provided with the distribution.
Neither the name of the Stanford University nor the names of its contributors 
may be used to endorse or promote products derived from this software without 
specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND 
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED 
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR 
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES 
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; 
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON 
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT 
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS 
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/



// input port controller
module vcr_ip_ctrl_mac
  (clk, reset, router_address, flit_ctrl_in, flit_data_in, route_port_ivc, 
   inc_rc_ivc, vc_req_ivc, vc_gnt_ivc, vc_gnt_ivc_ovc, sw_alloc_out_op, 
   sw_alloc_in_op, int_flit_ctrl_out, flit_data_out, flow_ctrl_out, 
   fbf_write_addr, fbf_write_enable, fbf_write_data, fbf_read_enable, 
   fbf_read_addr, fbf_read_data, error);
   
`include "c_functions.v"
`include "c_constants.v"
`include "vcr_constants.v"
   
   // flit buffer entries per VC
   parameter num_flit_buffers = 8;
   
   // width required to select individual buffer slot
   localparam flit_buffer_idx_width = clogb(num_flit_buffers);
   
   // maximum number of packets that can be in a given VC buffer simultaneously
   parameter num_header_buffers = 4;
   
   // number of message classes (e.g. request, reply)
   parameter num_message_classes = 2;
   
   // number of resource classes (e.g. minimal, adaptive)
   parameter num_resource_classes = 2;
   
   // total number of packet classes
   localparam num_packet_classes = num_message_classes * num_resource_classes;
   
   // number of VCs per class
   parameter num_vcs_per_class = 1;
   
   // number of VCs
   localparam num_vcs = num_packet_classes * num_vcs_per_class;
   
   // width required to select individual VC
   localparam vc_idx_width = clogb(num_vcs);
   
   // number of routers in each dimension
   parameter num_routers_per_dim = 4;
   
   // width required to select individual router in a dimension
   localparam dim_addr_width = clogb(num_routers_per_dim);
   
   // number of dimensions in network
   parameter num_dimensions = 2;
   
   // width required to select individual router in entire network
   localparam router_addr_width = num_dimensions * dim_addr_width;
   
   // number of nodes per router (a.k.a. consentration factor)
   parameter num_nodes_per_router = 1;
   
   // width required to select individual node at current router
   localparam node_addr_width = clogb(num_nodes_per_router);
   
   // width of global addresses
   localparam addr_width = router_addr_width + node_addr_width;
   
   // connectivity within each dimension
   parameter connectivity = `CONNECTIVITY_LINE;
   
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
   localparam port_idx_width = clogb(num_ports);
   
   // width required for lookahead routing information
   localparam la_route_info_width
     = port_idx_width + ((num_resource_classes > 1) ? 1 : 0);
   
   // select packet format
   parameter packet_format = `PACKET_FORMAT_EXPLICIT_LENGTH;
   
   // maximum payload length (in flits)
   // (note: only used if packet_format==`PACKET_FORMAT_EXPLICIT_LENGTH)
   parameter max_payload_length = 4;
   
   // minimum payload length (in flits)
   // (note: only used if packet_format==`PACKET_FORMAT_EXPLICIT_LENGTH)
   parameter min_payload_length = 1;
   
   // number of bits required to represent all possible payload sizes
   localparam payload_length_width
     = clogb(max_payload_length-min_payload_length+1);

   // total number of bits required for storing routing information
   localparam route_info_width
     = num_resource_classes * router_addr_width + node_addr_width;
   
   // total number of bits of header information encoded in header flit payload
   localparam header_info_width
     = (packet_format == `PACKET_FORMAT_HEAD_TAIL) ? 
       (la_route_info_width + route_info_width) : 
       (packet_format == `PACKET_FORMAT_EXPLICIT_LENGTH) ? 
       (la_route_info_width + route_info_width + payload_length_width) : 
       -1;
   
   // width of flit control signals
   localparam flit_ctrl_width
     = (packet_format == `PACKET_FORMAT_HEAD_TAIL) ? 
       (1 + vc_idx_width + 1 + 1) : 
       (packet_format == `PACKET_FORMAT_EXPLICIT_LENGTH) ? 
       (1 + vc_idx_width + 1) : 
       -1;
   
   // width of flit payload data
   parameter flit_data_width = 64;
   
   // width of flow control signals
   localparam flow_ctrl_width = 1 + vc_idx_width;
   
   // select whether to set a packet's outgoing VC ID at the input or output 
   // controller
   parameter track_vcs_at_output = 0;
   
   // filter out illegal destination ports
   // (the intent is to allow synthesis to optimize away the logic associated 
   // with such turns)
   parameter restrict_turns = 1;
   
   // select routing function type
   parameter routing_type = `ROUTING_TYPE_DOR;
   
   // select order of dimension traversal
   parameter dim_order = `DIM_ORDER_ASCENDING;
   
   // select method for credit signaling from output to input controller
   parameter int_flow_ctrl_type = `INT_FLOW_CTRL_TYPE_PUSH;
   
   // number of bits to be used for credit level reporting
   // (note: must be less than or equal to cred_count_width as given below)
   // (note: this parameter is only used for INT_FLOW_CTRL_TYPE_LEVEL)
   parameter cred_level_width = 2;
   
   // width required for internal flit control signalling
   localparam int_flit_ctrl_width = 1 + vc_idx_width + 1 + 1;
   
   // width required for internal flow control signalling
   localparam int_flow_ctrl_width
     = (int_flow_ctrl_type == `INT_FLOW_CTRL_TYPE_LEVEL) ?
       cred_level_width :
       (int_flow_ctrl_type == `INT_FLOW_CTRL_TYPE_PUSH) ?
       1 :
       -1;
   
   // select implementation variant for header FIFO
   parameter header_fifo_type = `FIFO_TYPE_INDEXED;
   
   // number of entries in flit buffer
   localparam fbf_depth = num_vcs*num_flit_buffers;
   
   // required address size for flit buffer
   localparam fbf_addr_width = clogb(fbf_depth);
   
   // select implementation variant for VC allocator
   parameter vc_alloc_type = `VC_ALLOC_TYPE_SEP_IF;
   
   // select whether VCs must have credits available in order to be considered 
   // for VC allocation
   parameter vc_alloc_requires_credit = 0;
   
   // select implementation variant for switch allocator
   parameter sw_alloc_type = `SW_ALLOC_TYPE_SEP_IF;
   
   // select which arbiter type to use for switch allocator
   parameter sw_alloc_arbiter_type = `ARBITER_TYPE_ROUND_ROBIN;
   
   // select speculation type for switch allocator
   parameter sw_alloc_spec_type = `SW_ALLOC_SPEC_TYPE_REQS_MASK_GNTS;
   
   // number of bits required for request signals
   localparam sw_alloc_req_width
     = (sw_alloc_spec_type == `SW_ALLOC_SPEC_TYPE_NONE) ? 1 : (1 + 1);
   
   // number of bits required for grant signals
   localparam sw_alloc_gnt_width = sw_alloc_req_width;
   
   // width of outgoing allocator control signals
   localparam sw_alloc_out_width
     = (sw_alloc_type == `SW_ALLOC_TYPE_SEP_IF) ? 
       sw_alloc_req_width : 
       (sw_alloc_type == `SW_ALLOC_TYPE_SEP_OF) ? 
       (sw_alloc_req_width + sw_alloc_gnt_width + 
	((sw_alloc_spec_type != `SW_ALLOC_SPEC_TYPE_NONE) ? 1 : 0)) : 
       ((sw_alloc_type >= `SW_ALLOC_TYPE_WF_BASE) && 
	(sw_alloc_type <= `SW_ALLOC_TYPE_WF_LIMIT)) ? 
       sw_alloc_req_width : 
       -1;
   
   // width of incoming allocator control signals
   localparam sw_alloc_in_width
     = (sw_alloc_type == `SW_ALLOC_TYPE_SEP_IF) ? 
       (sw_alloc_gnt_width + num_vcs*int_flow_ctrl_width) : 
       (sw_alloc_type == `SW_ALLOC_TYPE_SEP_OF) ? 
       (sw_alloc_gnt_width + num_vcs*int_flow_ctrl_width) :
       ((sw_alloc_type >= `SW_ALLOC_TYPE_WF_BASE) && 
	(sw_alloc_type <= `SW_ALLOC_TYPE_WF_LIMIT)) ? 
       (sw_alloc_gnt_width + num_vcs*int_flow_ctrl_width) : 
       -1;
   
   // enable performance counter
   parameter perf_ctr_enable = 1;
   
   // width of each counter
   parameter perf_ctr_width = 32;
   
   // configure error checking logic
   parameter error_capture_mode = `ERROR_CAPTURE_MODE_NO_HOLD;
   
   // width of sum of event signals from all VCs
   localparam event_width = clogb(num_vcs + 1);
   
   // ID of current input port
   parameter port_id = 0;
   
   parameter reset_type = `RESET_TYPE_ASYNC;
      
   input clk;
   input reset;
   
   // current router's address
   input [0:router_addr_width-1] router_address;
   
   // incoming flit control signals
   input [0:flit_ctrl_width-1] flit_ctrl_in;
   
   // incoming flit data
   input [0:flit_data_width-1] flit_data_in;
   
   // destination port
   output [0:num_vcs*port_idx_width-1] route_port_ivc;
   wire [0:num_vcs*port_idx_width-1] route_port_ivc;
   
   // transition to next resource class
   output [0:num_vcs-1] inc_rc_ivc;
   wire [0:num_vcs-1] 		     inc_rc_ivc;
   
   // request VC allocation
   output [0:num_vcs-1] vc_req_ivc;
   wire [0:num_vcs-1] 		     vc_req_ivc;
   
   // VC allocation successful
   input [0:num_vcs-1] vc_gnt_ivc;
   
   // granted output VC
   input [0:num_vcs*num_vcs-1] vc_gnt_ivc_ovc;
   
   // outgoing allocator contorl signals
   output [0:num_ports*sw_alloc_out_width-1] sw_alloc_out_op;
   wire [0:num_ports*sw_alloc_out_width-1] sw_alloc_out_op;
   
   // incoming allocator control signals
   input [0:num_ports*sw_alloc_in_width-1] sw_alloc_in_op;
   
   // outgoing flit control signals
   output [0:int_flit_ctrl_width-1] int_flit_ctrl_out;
   wire [0:int_flit_ctrl_width-1] 	   int_flit_ctrl_out;
   
   // outgoing flit data
   output [0:flit_data_width-1] flit_data_out;
   wire [0:flit_data_width-1] 		   flit_data_out;
   
   // outgoing flow control signals
   output [0:flow_ctrl_width-1] flow_ctrl_out;
   wire [0:flow_ctrl_width-1] 		   flow_ctrl_out;
   
   // flit buffer write address
   output [0:fbf_addr_width-1] fbf_write_addr;
   wire [0:fbf_addr_width-1] 		   fbf_write_addr;
   
   // flit buffer write enable
   output fbf_write_enable;
   wire 				   fbf_write_enable;
   
   // flit buffer write data
   output [0:flit_data_width-1] fbf_write_data;
   wire [0:flit_data_width-1] 		   fbf_write_data;
   
   // flit buffer read enable
   output fbf_read_enable;
   wire 				   fbf_read_enable;
   
   // flit buffer read address
   output [0:fbf_addr_width-1] fbf_read_addr;
   wire [0:fbf_addr_width-1] 		   fbf_read_addr;
   
   // flit buffer read data
   input [0:flit_data_width-1] fbf_read_data;
   
   // internal error condition detected
   output error;
   wire 				   error;
   
   
   //---------------------------------------------------------------------------
   // pack / unpack switch allocator control signals
   //---------------------------------------------------------------------------
   
   // non-speculative switch allocation requests to output-side arbitration 
   // stage / wavefront block
   wire [0:num_ports-1] 		   sw_oreq_nonspec_op;
   
   // speculative switch allocation requests to output-side arbitration stage or
   // wavefront block
   wire [0:num_ports-1] 		   sw_oreq_spec_op;
   
   // non-speculative switch allocation grants from output-side arbitration 
   // stage / wavefront block
   wire [0:num_ports-1] 		   sw_ognt_nonspec_op;
   
   // speculative switch allocation grants from output-side arbitration stage or
   // wavefront block
   wire [0:num_ports-1] 		   sw_ognt_spec_op;
   
   // non-speculative switch allocation grants to output-side arbitration stage 
   // or wavefront block
   wire [0:num_ports-1] 		   sw_ignt_nonspec_op;
   
   // speculative switch allocation grants to output-side arbitration stage or 
   // wavefront block
   wire [0:num_ports-1] 		   sw_ignt_spec_op;
   
   // internal flow control signalling from output controller to input 
   // controllers
   wire [0:num_ports*num_vcs*int_flow_ctrl_width-1] int_flow_ctrl_op_ovc;
   
   genvar 					    op;
   
   generate
      
      for(op = 0; op < num_ports; op = op + 1)
	begin:ops
	   
	   wire [0:sw_alloc_in_width-1] sw_alloc_in;
	   assign sw_alloc_in
	     = sw_alloc_in_op[op*sw_alloc_in_width:
			      (op+1)*sw_alloc_in_width-1];
	   
	   wire [0:sw_alloc_out_width-1] sw_alloc_out;
	   
	   assign sw_alloc_out[0] = sw_oreq_nonspec_op[op];
	   
	   if(sw_alloc_spec_type != `SW_ALLOC_SPEC_TYPE_NONE)
	     assign sw_alloc_out[1] = sw_oreq_spec_op[op];
	   
	   assign sw_ognt_nonspec_op[op] = sw_alloc_in[0];
	   
	   if(sw_alloc_spec_type != `SW_ALLOC_SPEC_TYPE_NONE)
	     assign sw_ognt_spec_op[op] = sw_alloc_in[1];
	   else
	     assign sw_ognt_spec_op[op] = 1'b0;
	   
	   if(sw_alloc_type == `SW_ALLOC_TYPE_SEP_IF)
	     begin
		
		assign int_flow_ctrl_op_ovc[op*num_vcs*
					    int_flow_ctrl_width:
					    (op+1)*num_vcs*
					    int_flow_ctrl_width-1]
			 = sw_alloc_in[sw_alloc_gnt_width:
				       sw_alloc_gnt_width+
				       num_vcs*int_flow_ctrl_width-1];
		
	     end
	   else if(sw_alloc_type == `SW_ALLOC_TYPE_SEP_OF)
	     begin
		
		assign sw_alloc_out[sw_alloc_req_width]
			 = sw_ignt_nonspec_op[op];
		
		if(sw_alloc_spec_type != `SW_ALLOC_SPEC_TYPE_NONE)
		  assign sw_alloc_out[sw_alloc_req_width+1]
			   = sw_ignt_spec_op[op];
		
		assign int_flow_ctrl_op_ovc[op*num_vcs*
					    int_flow_ctrl_width:
					    (op+1)*num_vcs*
					    int_flow_ctrl_width-1]
			 = sw_alloc_in[sw_alloc_gnt_width:
				       sw_alloc_gnt_width+
				       num_vcs*int_flow_ctrl_width-1];
		
		if(sw_alloc_spec_type != `SW_ALLOC_SPEC_TYPE_NONE)
		  begin
		     
		     wire sw_gnt;
		     assign sw_gnt
		       = sw_ignt_nonspec_op[op] | sw_ignt_spec_op[op];
		     
		     assign sw_alloc_out[sw_alloc_req_width+sw_alloc_gnt_width]
			      = sw_gnt;
		     
		  end
		
	     end
	   else if((sw_alloc_type >= `SW_ALLOC_TYPE_WF_BASE) &&
		   (sw_alloc_type <= `SW_ALLOC_TYPE_WF_LIMIT))
	     begin
		
		assign int_flow_ctrl_op_ovc[op*num_vcs*
					    int_flow_ctrl_width:
					    (op+1)*num_vcs*
					    int_flow_ctrl_width-1]
			 = sw_alloc_in[sw_alloc_gnt_width:
				       sw_alloc_gnt_width+
				       num_vcs*int_flow_ctrl_width-1];
		
	     end
	   
	   assign sw_alloc_out_op[op*sw_alloc_out_width:
				  (op+1)*sw_alloc_out_width-1]
		    = sw_alloc_out;
	   
	end
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // input stage
   //---------------------------------------------------------------------------
   
   wire 			     flit_valid_in;
   
   wire [0:flit_ctrl_width-1] 	     flit_ctrl_in_s, flit_ctrl_in_q;
   assign flit_ctrl_in_s = flit_ctrl_in;
   
   generate
      
      case(packet_format)
	
	`PACKET_FORMAT_HEAD_TAIL,
	`PACKET_FORMAT_EXPLICIT_LENGTH:
	  begin
	     
	     wire flit_valid_in_s, flit_valid_in_q;
	     assign flit_valid_in_s = flit_ctrl_in_s[0];
	     c_dff
	       #(.width(1),
		 .reset_type(reset_type))
	     flit_valid_inq
	       (.clk(clk),
		.reset(reset),
		.d(flit_valid_in_s),
		.q(flit_valid_in_q));
	     
	     assign flit_valid_in = flit_valid_in_q;
	     
	     assign flit_ctrl_in_q[0] = flit_valid_in_q;
	     
	     c_dff
	       #(.width(flit_ctrl_width-1),
		 .reset_type(reset_type))
	     flit_ctrl_inq
	       (.clk(clk),
		.reset(1'b0),
		.d(flit_ctrl_in_s[1:flit_ctrl_width-1]),
		.q(flit_ctrl_in_q[1:flit_ctrl_width-1]));
	     
	  end
	
      endcase
      
   endgenerate
   
   wire [0:flit_data_width-1] flit_data_in_s, flit_data_in_q;
   assign flit_data_in_s = flit_data_in;
   c_dff
     #(.width(flit_data_width),
       .reset_type(reset_type))
   flit_data_inq
     (.clk(clk),
      .reset(1'b0),
      .d(flit_data_in_s),
      .q(flit_data_in_q));
   
   
   //---------------------------------------------------------------------------
   // input vc controllers
   //---------------------------------------------------------------------------
   
   // extract header information from header data
   wire [0:header_info_width-1] header_info_in_q;
   assign header_info_in_q = flit_data_in_q[0:header_info_width-1];
   
   // non-speculative switch allocator requests
   wire [0:num_vcs-1] 		sw_req_nonspec_ivc;
   
   // non-speculative switch allocator grants
   wire [0:num_vcs-1] 		sw_gnt_nonspec_ivc;
   
   // speculative switch allocator requests
   wire [0:num_vcs-1] 		sw_req_spec_ivc;
   
   // speculative switch allocator grants
   wire [0:num_vcs-1] 		sw_gnt_spec_ivc;
   
   wire [0:num_vcs-1] 		flit_valid_in_ivc;
   wire [0:num_vcs-1] 		flit_head_in_ivc;
   wire [0:num_vcs-1] 		flit_tail_in_ivc;
   vcr_flit_ctrl_dec
     #(.num_message_classes(num_message_classes),
       .num_resource_classes(num_resource_classes),
       .num_vcs_per_class(num_vcs_per_class),
       .num_routers_per_dim(num_routers_per_dim),
       .num_dimensions(num_dimensions),
       .num_nodes_per_router(num_nodes_per_router),
       .connectivity(connectivity),
       .packet_format(packet_format),
       .max_payload_length(max_payload_length),
       .min_payload_length(min_payload_length),
       .reset_type(reset_type))
   fcdec
     (.clk(clk),
      .reset(reset),
      .flit_ctrl_in(flit_ctrl_in_q),
      .header_info_in(header_info_in_q),
      .flit_valid_out_ivc(flit_valid_in_ivc),
      .flit_head_out_ivc(flit_head_in_ivc),
      .flit_tail_out_ivc(flit_tail_in_ivc));
   
   wire [0:num_vcs-1] 		flit_head_ivc;
   wire [0:num_vcs-1] 		flit_tail_ivc;
   wire [0:num_vcs*num_ports-1] route_ivc_op;
   wire [0:num_vcs*la_route_info_width-1] la_route_info_ivc;
   wire [0:num_vcs*fbf_addr_width-1] 	  fbc_write_addr_ivc;
   wire [0:num_vcs*fbf_addr_width-1] 	  fbc_read_addr_ivc;
   wire [0:num_vcs*flit_buffer_idx_width-1] fbc_write_offset_ivc;
   wire [0:num_vcs*flit_buffer_idx_width-1] fbc_read_offset_ivc;
   wire [0:num_vcs-1] 			    fbc_empty_ivc;
   wire [0:num_vcs-1] 			    allocated_ivc;
   wire [0:num_vcs*num_vcs-1] 		    allocated_ivc_ovc;
   wire [0:num_vcs-1] 			    free_unallocated_ivc;
   wire [0:num_vcs-1] 			    free_allocated_ivc;
   wire [0:num_vcs*7-1] 		    ivcc_errors_ivc;
   wire [0:num_vcs*8-1] 		    events_ivc;
   
   genvar 				    ivc;
   
   generate
      
      for(ivc = 0; ivc < num_vcs; ivc = ivc + 1)
	begin:ivcs
	   
	   wire flit_valid_in;
	   assign flit_valid_in = flit_valid_in_ivc[ivc];
	   
	   wire flit_head_in;
	   assign flit_head_in = flit_head_in_ivc[ivc];
	   
	   wire flit_tail_in;
	   assign flit_tail_in = flit_tail_in_ivc[ivc];
	   
	   wire vc_gnt;
	   assign vc_gnt = vc_gnt_ivc[ivc];
	   
	   wire [0:num_vcs-1] vc_gnt_ovc;
	   assign vc_gnt_ovc = vc_gnt_ivc_ovc[ivc*num_vcs:(ivc+1)*num_vcs-1];
	   
	   wire 	      sw_gnt_nonspec;
	   assign sw_gnt_nonspec = sw_gnt_nonspec_ivc[ivc];
	   
	   wire               sw_gnt_spec;
	   assign sw_gnt_spec = sw_gnt_spec_ivc[ivc];
	   
	   wire [0:num_ports-1] route_op;
	   wire [0:port_idx_width-1] route_port;
	   wire 		     inc_rc;
	   wire 		     vc_req;
	   wire 		     sw_req_nonspec;
	   wire 		     sw_req_spec;
	   wire 		     flit_head;
	   wire 		     flit_tail;
	   wire [0:la_route_info_width-1] la_route_info;
	   wire [0:fbf_addr_width-1] 	  fbc_write_addr;
	   wire [0:fbf_addr_width-1] 	  fbc_read_addr;
	   wire 			  fbc_empty;
	   wire 			  allocated;
	   wire [0:num_vcs-1] 		  allocated_ovc;
	   wire 			  free_unallocated;
	   wire 			  free_allocated;
	   wire [0:6] 			  ivcc_errors;
	   wire [0:7] 			  events;
	   vcr_ivc_ctrl
	     #(.num_flit_buffers(num_flit_buffers),
	       .num_header_buffers(num_header_buffers),
	       .num_message_classes(num_message_classes),
	       .num_resource_classes(num_resource_classes),
	       .num_vcs_per_class(num_vcs_per_class),
	       .num_routers_per_dim(num_routers_per_dim),
	       .num_dimensions(num_dimensions),
	       .num_nodes_per_router(num_nodes_per_router),
	       .connectivity(connectivity),
	       .packet_format(packet_format),
	       .max_payload_length(max_payload_length),
	       .min_payload_length(min_payload_length),
	       .track_vcs_at_output(track_vcs_at_output),
	       .restrict_turns(restrict_turns),
	       .routing_type(routing_type),
	       .dim_order(dim_order),
	       .int_flow_ctrl_type(int_flow_ctrl_type),
	       .cred_level_width(cred_level_width),
	       .header_fifo_type(header_fifo_type),
	       .vc_alloc_type(vc_alloc_type),
	       .vc_alloc_requires_credit(vc_alloc_requires_credit),
	       .sw_alloc_spec_type(sw_alloc_spec_type),
	       .perf_ctr_enable(perf_ctr_enable),
	       .vc_id(ivc),
	       .port_id(port_id),
	       .reset_type(reset_type))
	   ivcc
	     (.clk(clk),
	      .reset(reset),
	      .router_address(router_address),
	      .flit_valid_in(flit_valid_in),
	      .flit_head_in(flit_head_in),
	      .flit_tail_in(flit_tail_in),
	      .header_info_in(header_info_in_q),
	      .int_flow_ctrl_op_ovc(int_flow_ctrl_op_ovc),
	      .route_op(route_op),
	      .route_port(route_port),
	      .inc_rc(inc_rc),
	      .vc_req(vc_req),
	      .vc_gnt(vc_gnt),
	      .vc_gnt_ovc(vc_gnt_ovc),
	      .sw_req_nonspec(sw_req_nonspec),
	      .sw_req_spec(sw_req_spec),
	      .sw_gnt_nonspec(sw_gnt_nonspec),
	      .sw_gnt_spec(sw_gnt_spec),
	      .flit_head(flit_head),
	      .flit_tail(flit_tail),
	      .la_route_info(la_route_info),
	      .fbc_write_addr(fbc_write_addr),
	      .fbc_read_addr(fbc_read_addr),
	      .fbc_empty(fbc_empty),
	      .allocated(allocated),
	      .allocated_ovc(allocated_ovc),
	      .free_unallocated(free_unallocated),
	      .free_allocated(free_allocated),
	      .errors(ivcc_errors),
	      .events(events));
	   
	   assign route_ivc_op[ivc*num_ports:(ivc+1)*num_ports-1]
		    = route_op;
	   assign route_port_ivc[ivc*port_idx_width:(ivc+1)*port_idx_width-1]
		    = route_port;
	   assign inc_rc_ivc[ivc] = inc_rc;
	   assign vc_req_ivc[ivc] = vc_req;
	   assign sw_req_nonspec_ivc[ivc] = sw_req_nonspec;
	   assign sw_req_spec_ivc[ivc] = sw_req_spec;
	   assign flit_head_ivc[ivc] = flit_head;
	   assign flit_tail_ivc[ivc] = flit_tail;
	   assign la_route_info_ivc[ivc*la_route_info_width:
				    (ivc+1)*la_route_info_width-1]
		    = la_route_info;
	   assign fbc_write_addr_ivc[ivc*fbf_addr_width:
				     (ivc+1)*fbf_addr_width-1]
		    = fbc_write_addr;
	   assign fbc_read_addr_ivc[ivc*fbf_addr_width:
				    (ivc+1)*fbf_addr_width-1]
		    = fbc_read_addr;
	   assign fbc_write_offset_ivc[ivc*flit_buffer_idx_width:
				       (ivc+1)*flit_buffer_idx_width-1]
		    = fbc_write_addr[fbf_addr_width-flit_buffer_idx_width:
				     fbf_addr_width-1];
	   assign fbc_read_offset_ivc[ivc*flit_buffer_idx_width:
				      (ivc+1)*flit_buffer_idx_width-1]
		    = fbc_read_addr[fbf_addr_width-flit_buffer_idx_width:
				    fbf_addr_width-1];
	   assign fbc_empty_ivc[ivc] = fbc_empty;
	   assign allocated_ivc[ivc] = allocated;
	   assign allocated_ivc_ovc[ivc*num_vcs:(ivc+1)*num_vcs-1]
		    = allocated_ovc;
	   assign free_unallocated_ivc[ivc] = free_unallocated;
	   assign free_allocated_ivc[ivc] = free_allocated;
	   assign ivcc_errors_ivc[ivc*7:(ivc+1)*7-1] = ivcc_errors;
	   assign events_ivc[ivc*8:(ivc+1)*8-1] = events;
	   
	end
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // switch allocation
   //---------------------------------------------------------------------------
   
   wire [0:num_vcs-1] 			  sw_req_nonspec_qual_ivc;
   assign sw_req_nonspec_qual_ivc = sw_req_nonspec_ivc & free_allocated_ivc;
   
   wire [0:num_vcs-1] 				    vc_sel_nonspec_ivc;
   
   vcr_sw_alloc_ip
     #(.num_vcs(num_vcs),
       .num_ports(num_ports),
       .allocator_type(sw_alloc_type),
       .arbiter_type(sw_alloc_arbiter_type),
       .reset_type(reset_type))
   swa_nonspec
     (.clk(clk),
      .reset(reset),
      .route_ivc_op(route_ivc_op),
      .req_in_ivc(sw_req_nonspec_qual_ivc),
      .req_out_op(sw_oreq_nonspec_op),
      .gnt_in_op(sw_ognt_nonspec_op),
      .gnt_out_ivc(sw_gnt_nonspec_ivc),
      .gnt_out_op(sw_ignt_nonspec_op),
      .sel_ivc(vc_sel_nonspec_ivc),
      .allow_update(1'b1));
   
   
   //---------------------------------------------------------------------------
   // speculative switch allocation support
   //---------------------------------------------------------------------------
   
   // any non-speculative grants?
   wire 					    sw_ognt_nonspec;
   assign sw_ognt_nonspec = |sw_ognt_nonspec_op;
   
   wire [0:num_vcs-1] 				    vc_sel_spec_ivc;
   wire [0:num_vcs-1] 				    vc_sel_ivc;
   wire 					    flit_sent;
   
   generate
      
      if(sw_alloc_spec_type != `SW_ALLOC_SPEC_TYPE_NONE)
	begin
	   
	   // any non-speculative requests?
	   wire sw_req_nonspec;
	   assign sw_req_nonspec = |sw_req_nonspec_ivc;
	   
	   wire [0:num_ports-1] sw_oreq_spec_unqual_op;
	   wire [0:num_vcs-1] 	sw_gnt_spec_unqual_ivc;
	   wire [0:num_ports-1] sw_ignt_spec_unqual_op;
	   wire 		allow_update_spec;
	   vcr_sw_alloc_ip
	     #(.num_vcs(num_vcs),
	       .num_ports(num_ports),
	       .allocator_type(sw_alloc_type),
	       .arbiter_type(sw_alloc_arbiter_type),
	       .reset_type(reset_type))
	   swa_spec
	     (.clk(clk),
	      .reset(reset),
	      .route_ivc_op(route_ivc_op),
	      .req_in_ivc(sw_req_spec_ivc),
	      .req_out_op(sw_oreq_spec_unqual_op),
	      .gnt_in_op(sw_ognt_spec_op),
	      .gnt_out_ivc(sw_gnt_spec_unqual_ivc),
	      .gnt_out_op(sw_ignt_spec_unqual_op),
	      .sel_ivc(vc_sel_spec_ivc),
	      .allow_update(allow_update_spec));
	   
	   wire [0:num_vcs-1] 	sw_gnt_spec_qual_ivc;
	   if(vc_alloc_requires_credit)
	     assign sw_gnt_spec_qual_ivc = sw_gnt_spec_unqual_ivc;
	   else
	     assign sw_gnt_spec_qual_ivc
	       = sw_gnt_spec_unqual_ivc & free_unallocated_ivc;
	   
	   if(sw_alloc_type == `SW_ALLOC_TYPE_SEP_IF)
	     begin
		
		// no additional masking is required for speculative grants
		assign sw_gnt_spec_ivc = sw_gnt_spec_qual_ivc;
		assign sw_ignt_spec_op = sw_ignt_spec_unqual_op;
		
		// updates are masked indirectly via output-side grants
		assign allow_update_spec = 1'b1;
		
		case(sw_alloc_spec_type)
		  
		  `SW_ALLOC_SPEC_TYPE_REQS_MASK_REQS,
		  `SW_ALLOC_SPEC_TYPE_REQS_MASK_GNTS:
		    begin
		       
		       // do not propagate speculative requests if any 
		       // non-speculative requests were issued by this input's 
		       // VCs
		       assign sw_oreq_spec_op = {num_ports{~sw_req_nonspec}} & 
						sw_oreq_spec_unqual_op;
		       
		       // flit buffer read address can be determined early
		       assign vc_sel_ivc = sw_req_nonspec ? 
					   vc_sel_nonspec_ivc : 
					   vc_sel_spec_ivc;
		       
		    end
		  
		  `SW_ALLOC_SPEC_TYPE_GNTS_MASK_REQS,
		  `SW_ALLOC_SPEC_TYPE_GNTS_MASK_GNTS:
		    begin
		       
		       // do not propagate requests if a non-speculative grant 
		       // was generated for this input
		       assign sw_oreq_spec_op = {num_ports{~sw_ognt_nonspec}} & 
						sw_oreq_spec_unqual_op;
		       
		       // we only know whether to use non-speculative or 
		       // speculative flits once the output-side grants are 
		       // known
		       assign vc_sel_ivc = sw_ognt_nonspec ? 
					   vc_sel_nonspec_ivc : 
					   vc_sel_spec_ivc;
		       
		    end
		  
		endcase
		
	     end
	   else if(sw_alloc_type == `SW_ALLOC_TYPE_SEP_OF)
	     begin
		
		case(sw_alloc_spec_type)
		  
		  `SW_ALLOC_SPEC_TYPE_REQS_MASK_GNTS:
		    begin
		       
		       // propagate all speculative requests
		       assign sw_oreq_spec_op = sw_oreq_spec_unqual_op;
		       
		       // suppress speculative grants if any non-speculative 
		       // requests were issued by this input's VCs
		       assign sw_gnt_spec_ivc = {num_vcs{~sw_req_nonspec}} & 
						sw_gnt_spec_qual_ivc;
		       assign sw_ignt_spec_op = {num_ports{~sw_req_nonspec}} & 
						sw_ignt_spec_unqual_op;
		       
		       // flit buffer read address can be determined early
		       assign vc_sel_ivc = sw_req_nonspec ? 
					   vc_sel_nonspec_ivc : 
					   vc_sel_spec_ivc;
		       
		       // inhibit updates if non-speculative requests are 
		       // present
		       assign allow_update_spec = ~sw_req_nonspec;
		       
		    end
		  
		  `SW_ALLOC_SPEC_TYPE_REQS_MASK_REQS:
		    begin
		       
		       // do not propagate speculative requests if any 
		       // non-speculative requests were issued by this input's 
		       // VCs
		       assign sw_oreq_spec_op = {num_ports{~sw_req_nonspec}} & 
						sw_oreq_spec_unqual_op;
		       
		       // no additional masking is required for speculative 
		       // grants
		       assign sw_gnt_spec_ivc = sw_gnt_spec_qual_ivc;
		       assign sw_ignt_spec_op = sw_ignt_spec_unqual_op;
		       
		       // flit buffer read address can be determined early
		       assign vc_sel_ivc = sw_req_nonspec ? 
					   vc_sel_nonspec_ivc : 
					   vc_sel_spec_ivc;
		       
		       // updates are masked indirectly via output-side grants
		       assign allow_update_spec = 1'b1;
		       
		    end
		  
		  `SW_ALLOC_SPEC_TYPE_GNTS_MASK_GNTS:
		    begin
		       
		       // propagate all speculative requests
		       assign sw_oreq_spec_op = sw_oreq_spec_unqual_op;
		       
		       // suppress speculative grants if any non-speculative 
		       // grants were generated for this input
		       assign sw_gnt_spec_ivc = {num_vcs{~sw_ognt_nonspec}} & 
						sw_gnt_spec_qual_ivc;
		       assign sw_ignt_spec_op = {num_ports{~sw_ognt_nonspec}} & 
						sw_ignt_spec_unqual_op;
		       
		       // we only know whether to use non-speculative or 
		       // speculative flits once the output-side grants are 
		       // known
		       assign vc_sel_ivc = sw_ognt_nonspec ? 
					   vc_sel_nonspec_ivc : 
					   vc_sel_spec_ivc;
		       
		       // inhibit updates if non-speculative grants are present
		       assign allow_update_spec = ~sw_ognt_nonspec;
		       
		    end
		  
		  `SW_ALLOC_SPEC_TYPE_GNTS_MASK_REQS:
		    begin
		       
		       // do not propagate requests if a non-speculative grant 
		       // was generated for this input
		       assign sw_oreq_spec_op = {num_ports{~sw_ognt_nonspec}} & 
						sw_oreq_spec_unqual_op;
		       
		       // no additional masking requiured for grants
		       assign sw_gnt_spec_ivc = sw_gnt_spec_qual_ivc;
		       assign sw_ignt_spec_op = sw_ignt_spec_unqual_op;
		       
		       // we only know whether to use non-speculative or 
		       // speculative flits once the output-side grants are 
		       // known
		       assign vc_sel_ivc = sw_ognt_nonspec ? 
					   vc_sel_nonspec_ivc : 
					   vc_sel_spec_ivc;
		       
		       // updates are masked indirectly via output-side grants
		       assign allow_update_spec = 1'b1;
		       
		    end
		  
		endcase
		
	     end
	   else if((sw_alloc_type >= `SW_ALLOC_TYPE_WF_BASE) &&
		   (sw_alloc_type <= `SW_ALLOC_TYPE_WF_LIMIT))
	     begin
		
		// propagate all speculative requests
		assign sw_oreq_spec_op = sw_oreq_spec_unqual_op;
		
		// all masking is done inside the wavefront block, so we can 
		// just propagate grants
		assign sw_gnt_spec_ivc = sw_gnt_spec_qual_ivc;
		assign sw_ignt_spec_op = sw_ignt_spec_unqual_op;
		
		case(sw_alloc_spec_type)
		  
		  `SW_ALLOC_SPEC_TYPE_REQS_MASK_REQS,
		  `SW_ALLOC_SPEC_TYPE_REQS_MASK_GNTS:
		    begin
		       
		       // flit buffer read address can be determined early
		       assign vc_sel_ivc = sw_req_nonspec ? 
					   vc_sel_nonspec_ivc : 
					   vc_sel_spec_ivc;
		       
		       // inhibit updates if non-speculative requests are 
		       // present
		       assign allow_update_spec = ~sw_req_nonspec;
		       
		    end
		  
		  `SW_ALLOC_SPEC_TYPE_GNTS_MASK_REQS,
		  `SW_ALLOC_SPEC_TYPE_GNTS_MASK_GNTS:
		    begin
		       
		       // flit buffer read address depends on whether a 
		       // non-speculative grant was generated
		       assign vc_sel_ivc = sw_ognt_nonspec ? 
					   vc_sel_nonspec_ivc : 
					   vc_sel_spec_ivc;
		       
		       // inhibit updates if non-speculative grant was generated
		       assign allow_update_spec = ~sw_ognt_nonspec;
		       
		    end
		  
		endcase
		
	     end
	   
	   // if any output port was granted to this input port 
	   // non-speculatively, we know that a flit will be sent out
	   wire flit_sent_nonspec;
	   assign flit_sent_nonspec = sw_ognt_nonspec;
	   
	   // for speculative grants, on the other hand, we must make sure that 
	   // no misspeculation occurred
	   wire 	      flit_sent_spec;
	   
	   if(sw_alloc_type == `SW_ALLOC_TYPE_SEP_IF)
	     begin
		
		// for the separable input-first case, we can use a shortcut by 
		// exploiting the fact that the VC select signal is available 
		// early
		if(vc_alloc_requires_credit)
		  assign flit_sent_spec
		    = |sw_ognt_spec_op & |(vc_gnt_ivc & vc_sel_spec_ivc);
		else
		  assign flit_sent_spec
		    = |sw_ognt_spec_op & 
		      |(vc_gnt_ivc & vc_sel_spec_ivc & free_unallocated_ivc);
		
	     end
	   else if((sw_alloc_type == `SW_ALLOC_TYPE_SEP_OF) || 
		   ((sw_alloc_type >= `SW_ALLOC_TYPE_WF_BASE) && 
		    (sw_alloc_type <= `SW_ALLOC_TYPE_WF_LIMIT)))
	     begin
		
		// for separable output-first and wavefront allocation, we don't
		// know until the end of allocation which input-side VC was 
		// selected
		assign flit_sent_spec = |(vc_gnt_ivc & sw_gnt_spec_ivc);
		
	     end
	   
	   // at most one of the two should ever be active
	   assign flit_sent = flit_sent_nonspec | flit_sent_spec;
	   
	end
      else
	begin
	   
	   // if speculation is disabled, tie controls signals to zero
	   assign sw_gnt_spec_ivc = {num_vcs{1'b0}};
	   assign sw_oreq_spec_op = {num_ports{1'b0}};
	   assign sw_ignt_spec_op = {num_ports{1'b0}};
	   
	   // selector for flit buffer read address
	   assign vc_sel_spec_ivc = {num_vcs{1'b0}};
	   assign vc_sel_ivc = vc_sel_nonspec_ivc;
	   
	   // in the absence of speculation, if any output port was granted to 
	   // this input port, we know that a flit will be sent out
	   assign flit_sent = sw_ognt_nonspec;
	   
	end
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // flit buffer control signals
   //---------------------------------------------------------------------------
   
   generate
      
      if(num_vcs > 1)
	begin
	   
	   wire [0:vc_idx_width-1] flit_vc_in_q;
	   assign flit_vc_in_q = flit_ctrl_in_q[1:1+vc_idx_width-1];
	   
	   // use optimized version for power-of-two buffer sizes
	   if(num_flit_buffers == (1 << flit_buffer_idx_width))
	     begin
		
		wire [0:flit_buffer_idx_width-1] fbf_write_offset;
		assign fbf_write_offset
		  = fbc_write_offset_ivc[flit_vc_in_q*flit_buffer_idx_width +:
					 flit_buffer_idx_width];
		
		wire [0:vc_idx_width-1] 	 fbf_write_base;
		assign fbf_write_base = flit_vc_in_q;
		
		assign fbf_write_addr = {fbf_write_base, fbf_write_offset};
		
		wire [0:flit_buffer_idx_width-1] fbf_read_offset;
		c_select_1ofn
		  #(.num_ports(num_vcs),
		    .width(flit_buffer_idx_width))
		fbf_read_offset_sel
		  (.select(vc_sel_ivc),
		   .data_in(fbc_read_offset_ivc),
		   .data_out(fbf_read_offset));
		
		wire [0:vc_idx_width-1] 	 fbf_read_base;
		c_encoder
		  #(.num_ports(num_vcs))
		fbf_read_base_enc
		  (.data_in(vc_sel_ivc),
		   .data_out(fbf_read_base));
		
		assign fbf_read_addr = {fbf_read_base, fbf_read_offset};
		
	     end
	   
	   // in the general case, mux in the full address
	   else
	     begin
		
		assign fbf_write_addr
		  = fbc_write_addr_ivc[flit_vc_in_q*fbf_addr_width +:
				       fbf_addr_width];
		
		c_select_1ofn
		  #(.num_ports(num_vcs),
		    .width(fbf_addr_width))
		fbf_read_addr_sel
		  (.select(vc_sel_ivc),
		   .data_in(fbc_read_addr_ivc),
		   .data_out(fbf_read_addr));
		
	     end
	   
	end
      
      // if we only have a single VC, just use the address as is
      else
	begin
	   assign fbf_write_addr = fbc_write_addr_ivc;
	   assign fbf_read_addr = fbc_read_addr_ivc;
	end
      
   endgenerate
   
   assign fbf_write_enable = flit_valid_in;
   assign fbf_read_enable = flit_sent;
   assign fbf_write_data = flit_data_in_q;
   
   
   //---------------------------------------------------------------------------
   // generate outputs to switch
   //---------------------------------------------------------------------------
   
   wire 			     flit_head_muxed;
   c_select_1ofn
     #(.num_ports(num_vcs),
       .width(1))
   flit_head_muxed_sel
     (.select(vc_sel_ivc),
      .data_in(flit_head_ivc),
      .data_out(flit_head_muxed));
   
   wire 			     flit_tail_muxed;
   c_select_1ofn
     #(.num_ports(num_vcs),
       .width(1))
   flit_tail_muxed_sel
     (.select(vc_sel_ivc),
      .data_in(flit_tail_ivc),
      .data_out(flit_tail_muxed));
   
   wire [0:int_flit_ctrl_width-1]    int_flit_ctrl_out_s, int_flit_ctrl_out_q;
   assign int_flit_ctrl_out_s[0] = flit_sent;

   wire 			     flit_valid_out_s, flit_valid_out_q;
   assign flit_valid_out_s = int_flit_ctrl_out_s[0];
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   flit_valid_outq
     (.clk(clk),
      .reset(reset),
      .d(flit_valid_out_s),
      .q(flit_valid_out_q));
   
   generate
      
      if(num_vcs > 1)
	begin
	   
	   wire [0:vc_idx_width-1] flit_vc;
	   
	   if(track_vcs_at_output)
	     begin
		
		c_encoder
		  #(.num_ports(num_vcs))
		flit_vc_enc
		  (.data_in(vc_sel_ivc),
		   .data_out(flit_vc));
		
	     end
	   else
	     begin
		
		wire [0:num_vcs-1] allocated_ovc;
		c_select_1ofn
		  #(.num_ports(num_vcs),
		    .width(num_vcs))
		allocated_ovc_sel
		  (.select(vc_sel_nonspec_ivc),
		   .data_in(allocated_ivc_ovc),
		   .data_out(allocated_ovc));
		
		wire [0:num_vcs-1] dest_ovc;
		
		if(sw_alloc_spec_type != `SW_ALLOC_SPEC_TYPE_NONE)
		  begin
		     
		     wire allocated;
		     c_select_1ofn
		       #(.num_ports(num_vcs),
			 .width(1))
		     allocated_sel
		       (.select(vc_sel_ivc),
			.data_in(allocated_ivc),
			.data_out(allocated));
		     
		     wire [0:num_vcs-1] vc_gnt_ovc;
		     c_select_1ofn
		       #(.num_ports(num_vcs),
			 .width(num_vcs))
		     vc_gnt_ovc_sel
		       (.select(vc_sel_spec_ivc),
			.data_in(vc_gnt_ivc_ovc),
			.data_out(vc_gnt_ovc));
		     
		     assign dest_ovc = allocated ? allocated_ovc : vc_gnt_ovc;
		     
		  end
		else
		  assign dest_ovc = allocated_ovc;
		
		c_encoder
		  #(.num_ports(num_vcs))
		flit_vc_enc
		  (.data_in(dest_ovc),
		   .data_out(flit_vc));
		
	     end
	   
	   assign int_flit_ctrl_out_s[1:1+vc_idx_width-1] = flit_vc;
	   
	end
      
   endgenerate
   
   assign int_flit_ctrl_out_s[1+vc_idx_width] = flit_head_muxed;
   assign int_flit_ctrl_out_s[1+vc_idx_width+1] = flit_tail_muxed;
   c_dff
     #(.width(int_flit_ctrl_width-1),
       .reset_type(reset_type))
   int_flit_ctrl_outq
     (.clk(clk),
      .reset(1'b0),
      .d(int_flit_ctrl_out_s[1:int_flit_ctrl_width-1]),
      .q(int_flit_ctrl_out_q[1:int_flit_ctrl_width-1]));
   
   assign int_flit_ctrl_out_q[0] = flit_valid_out_q;
   
   wire flit_head_out_q;
   assign flit_head_out_q = int_flit_ctrl_out_q[1+vc_idx_width+0];
   
   assign int_flit_ctrl_out = int_flit_ctrl_out_q;
   
   wire [0:la_route_info_width-1] la_route_info_muxed;
   c_select_1ofn
     #(.num_ports(num_vcs),
       .width(la_route_info_width))
   la_route_info_muxed_sel
     (.select(vc_sel_ivc),
      .data_in(la_route_info_ivc),
      .data_out(la_route_info_muxed));
   
   wire [0:la_route_info_width-1] la_route_info_s, la_route_info_q;
   assign la_route_info_s = la_route_info_muxed;
   c_dff
     #(.width(la_route_info_width),
       .reset_type(reset_type))
   la_route_infoq
     (.clk(clk),
      .reset(1'b0),
      .d(la_route_info_s),
      .q(la_route_info_q));
   
   assign flit_data_out[0:la_route_info_width-1]
	    = flit_head_out_q ? 
	      la_route_info_q : 
	      fbf_read_data[0:la_route_info_width-1];
   assign flit_data_out[la_route_info_width:flit_data_width-1]
	    = fbf_read_data[la_route_info_width:flit_data_width-1];
   
   
   //---------------------------------------------------------------------------
   // generate outgoing credits
   //---------------------------------------------------------------------------
   
   wire 			     cred_valid_s, cred_valid_q;
   assign cred_valid_s = flit_sent;
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   cred_validq
     (.clk(clk),
      .reset(reset),
      .d(cred_valid_s),
      .q(cred_valid_q));
   
   assign flow_ctrl_out[0] = cred_valid_q;
   
   generate
      
      if(num_vcs > 1)
	begin
	   
	   wire [0:vc_idx_width-1] cred_vc;
	   c_encoder
	     #(.num_ports(num_vcs))
	   cred_vc_enc
	     (.data_in(vc_sel_ivc),
	      .data_out(cred_vc));
	   
	   wire [0:vc_idx_width-1] cred_vc_s, cred_vc_q;
	   assign cred_vc_s = cred_vc;
	   c_dff
	     #(.width(vc_idx_width),
	       .reset_type(reset_type))
	   cred_vcq
	     (.clk(clk),
	      .reset(1'b0),
	      .d(cred_vc_s),
	      .q(cred_vc_q));
	   
	   assign flow_ctrl_out[1:1+vc_idx_width-1] = cred_vc_q;
	   
	end
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // performance counters
   //---------------------------------------------------------------------------
   
   generate
      
      if(perf_ctr_enable > 0)
	begin
	   
	   wire [0:8*num_vcs-1] events;
	   c_interleaver
	     #(.width(num_vcs*8),
	       .num_blocks(num_vcs))
	   events_intl
	     (.data_in(events_ivc),
	      .data_out(events));
	   
	   genvar ctr;
	   
	   for(ctr = 0; ctr < 8; ctr = ctr + 1)
	     begin:ctrs
		
		wire [0:num_vcs-1] ctr_events;
		assign ctr_events = events[ctr*num_vcs:(ctr+1)*num_vcs-1];
		
		wire [0:event_width-1] ctr_sum;
		c_add_nto1
		  #(.num_ports(num_vcs),
		    .width(1))
		ctr_sum_add
		  (.data_in(ctr_events),
		   .data_out(ctr_sum));
		
		wire [0:perf_ctr_width-1] ctr_s, ctr_q;
		assign ctr_s = ctr_q + ctr_sum;
		c_dff
		  #(.width(perf_ctr_width),
		    .reset_type(reset_type))
		ctrq
		  (.clk(clk),
		   .reset(reset),
		   .d(ctr_s),
		   .q(ctr_q));
		
	     end
	end
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // error checking
   //---------------------------------------------------------------------------
   
   generate
      
      if(error_capture_mode != `ERROR_CAPTURE_MODE_NONE)
	begin
	   
	   // synopsys translate_off
	   
	   integer i;
	   
	   always @(posedge clk)
	     begin
		
		for(i = 0; i < num_vcs; i = i + 1)
		  begin
		     
		     if(ivcc_errors_ivc[i*7])
		       $display("ERROR: Flit buffer underflow in module %m.");
		     
		     if(ivcc_errors_ivc[i*7+1])
		       $display("ERROR: Flit buffer overflow in module %m.");
		     
		     if(ivcc_errors_ivc[i*7+2])
		       $display("ERROR: Head FIFO underflow in module %m.");
		     
		     if(ivcc_errors_ivc[i*7+3])
		       $display("ERROR: Head FIFO overflow in module %m.");
		     
		     if(ivcc_errors_ivc[i*7+4])
		       $display("ERROR: Stray flit received in module %m.");
		     
		     if(ivcc_errors_ivc[i*7+5])
		       $display("ERROR: Credit tracker overflow in module %m.");
		     
		     if(ivcc_errors_ivc[i*7+6])
		       $display({"ERROR: Received flit's destination does ",
				 "not match port constraints in module %m."});
		     
		  end
		
	     end
	   // synopsys translate_on
	   
	   wire [0:num_vcs*7-1] errors_s, errors_q;
	   assign errors_s = ivcc_errors_ivc;
	   c_err_rpt
	     #(.num_errors(num_vcs*7),
	       .capture_mode(error_capture_mode),
	       .reset_type(reset_type))
	   chk
	     (.clk(clk),
	      .reset(reset),
	      .errors_in(errors_s),
	      .errors_out(errors_q));
	   
	   assign error = |errors_q;
	   
	end
      else
	assign error = 1'b0;
      
   endgenerate
   
endmodule
